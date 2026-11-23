#include "state_machine.h"
#include <stdarg.h>
#include <stdio.h>
#include <string.h>
#include "bytes.h"
#include "command_flags.h"
#include "config.h"
#include "data_buffer.h"
#include "gui_link.h"
#include "motor_drive.h"

using namespace Config;

namespace {

const char* faultName(FaultReason reason) {
    switch (reason) {
        case FaultReason::SensorStalled:    return "sensor stalled";
        case FaultReason::SensorOutOfRange: return "sensor out of range";
        case FaultReason::DriveFault:       return "drive fault";
        case FaultReason::CycleTimeout:     return "no cycles detected";
        case FaultReason::ForceFailsafe:    return "excessive force";
        case FaultReason::StrokeNotFound:   return "stroke not found";
        case FaultReason::SafetyCircuit:    return "safety circuit open";
    }
    return "unknown";
}

const char* stateName(State state) {
    switch (state) {
        case State::Idle:        return "idle";
        case State::Panic:       return "panic";
        case State::Armed:       return "armed";
        case State::Calibration: return "calibrating";
        case State::Operating:   return "running";
        case State::Tar:         return "TAR";
    }
    return "unknown";
}

bool atRail(int32_t value) {
    return value >= INT24_MAX - ADC_RAIL_MARGIN || value <= INT24_MIN + ADC_RAIL_MARGIN;
}

long newtons(int64_t counts) {
    return (long)((float)counts * FORCE_NEWTON_PER_COUNT);
}

} // namespace

void StateMachine::begin(GuiLink* gui, MotorDrive* drive, PointBuffer* points) {
    gui_ = gui;
    drive_ = drive;
    points_ = points;
    state_ = State::Idle;
    forceMin_ = DEFAULT_FORCE_MIN_COUNTS;
    forceMax_ = DEFAULT_FORCE_MAX_COUNTS;
}

// =============================================================================
// Main loop entry points
// =============================================================================

void StateMachine::pollDrive() {
    char text[160];
    while (drive_->nextError(text, sizeof(text))) {
        gui_->sendText(Cmd::ESP_error, text);
        if (inMotionState()) {
            enterPanic(FaultReason::DriveFault);
        }
    }
}

void StateMachine::handleCommand(const Command& command, uint32_t nowMs) {
    // Any command shows the GUI is alive. A heartbeat from a GUI that wasn't linked means
    // the ESP lost the link (it rebooted, or it timed out and stopped the motor) and the
    // GUI doesn't know: tell it, it answers with GUI_check_connection.
    const bool wasLinked = guiLinked_;
    guiLinked_ = true;
    guiLastMs_ = nowMs;
    if (command.flag == Cmd::GUI_heartbeat) {
        if (!wasLinked) {
            gui_->send(Cmd::ESP_not_connected);
        }
        return;
    }

    switch (command.flag) {
        case Cmd::GUI_check_connection:
            abort();
            // The GUI just connected: the drive may have been powered up
            // since the boot, and the timeout is only written to its RAM.
            prepareDrive();
            gui_->send(Cmd::ESP_good_connection);
            if (state_ == State::Panic) { // tell the new GUI why nothing works
                report(Cmd::ESP_error, "PANIC: %s (press the reset button on the machine)",
                       faultName(panicReason_));
                gui_->send(Cmd::ESP_emergency);
            }
            break;
        case Cmd::GUI_abort:
            abort();
            break;
        case Cmd::GUI_run_request:
            onRunRequest(command, nowMs);
            break;
        case Cmd::GUI_calibrate:
            onCalibrate(nowMs);
            break;
        case Cmd::GUI_send_sequence:
            onSequenceStep(command);
            break;
        case Cmd::GUI_run_start:
            onRunStart(nowMs);
            break;
        case Cmd::GUI_TAR_request:
            onTarRequest(/*measureEmptyLoad=*/true, nowMs);
            break;
        case Cmd::GUI_bottom_out:
            onTarRequest(/*measureEmptyLoad=*/false, nowMs);
            break;
        case Cmd::GUI_set_rpm:
            onDebugDriveFrame(command);
            break;
        case Cmd::GUI_force_reboot:
            onReboot();
            break;
        case Cmd::GUI_set_failsafe:
            onSetFailsafe(command);
            break;
        default:
            report(Cmd::ESP_error, "Unknown command %u", (unsigned)command.flag);
            break;
    }
}

void StateMachine::onSample(const SensorSample& sample, uint32_t nowMs) {
    haveSample_ = true;
    lastSampleMs_ = nowMs;
    position_ = positionFilter_.update(sample.position);
    force_ = forceFilter_.update(sample.force);
    forceAverage_.update(sample.force);

    if (!inMotionState()) {
        return;
    }
    if (!safetyChecksPass()) {
        return; // went to Panic
    }
    switch (state_) {
        case State::Calibration:
            updateCalibration(nowMs);
            break;
        case State::Operating:
            updateOperating(sample, nowMs);
            break;
        case State::Tar:
            updateTar(nowMs);
            break;
        default:
            break;
    }
}

void StateMachine::tick(uint32_t nowMs) {
    if (guiLinked_ && nowMs - guiLastMs_ > GUI_TIMEOUT_MS) {
        onGuiLost(nowMs);
    }
    if (nowMs - lastLiveDataMs_ >= HEARTBEAT_PERIOD_MS) {
        lastLiveDataMs_ = nowMs;
        sendLiveData(nowMs);
    }
    if (inMotionState() && !sensorAlive(nowMs)) {
        enterPanic(FaultReason::SensorStalled, "no sample for %lu ms", (unsigned long)(nowMs - lastSampleMs_));
        return;
    }
    if (state_ == State::Armed && nowMs - armedSinceMs_ >= ARMED_TIMEOUT_MS) {
        denyRun("Run cancelled: not started within %lu s", (unsigned long)(ARMED_TIMEOUT_MS / 1000));
    }
}

// =============================================================================
// GUI commands
// =============================================================================

void StateMachine::onRunRequest(const Command& command, uint32_t nowMs) {
    if (state_ != State::Idle) {
        denyRun("Run refused: the machine is %s", stateName(state_));
        return;
    }
    if (safety_ != Safety::Closed) {
        denyRun("Run refused: safety circuit open (press the reset button on the machine)");
        return;
    }
    const unsigned count = command.payloadSize >= 1 ? command.payload[0] : 0;
    if (count == 0 || count > MAX_SEQUENCE_STEPS) {
        denyRun("Run refused: %u steps (1 to %u allowed)", count, (unsigned)MAX_SEQUENCE_STEPS);
        return;
    }
    if (!sensorAlive(nowMs)) {
        denyRun("Run refused: no data from the sensors");
        return;
    }
    memset(stepReceived_, 0, sizeof(stepReceived_));
    stepsExpected_ = (uint8_t)count;
    stepsReceived_ = 0;
    state_ = State::Armed;
    armedSinceMs_ = nowMs;
    gui_->send(Cmd::ESP_run_accepted);
}

// Run calibration, from Armed (the motor is stopped). See updateCalibration().
void StateMachine::onCalibrate(uint32_t nowMs) {
    if (state_ != State::Armed) {
        report(Cmd::ESP_error, "Calibration refused: the machine is %s", stateName(state_));
        return;
    }
    enterMotionState(State::Calibration, nowMs);
    if (!startMotor(CALIBRATION_CENTIHZ)) {
        return;
    }
    calibrationMotionStartMs_ = nowMs;
    turnFinder_.start(STROKE_TURN_HYSTERESIS_COUNTS, STROKE_MIN_RANGE_COUNTS);
    calibrationPhase_ = CalibrationPhase::FindStroke;
    report(Cmd::ESP_debug, "Calibration: looking for the stroke");
}

// Accepted in any state: the window only depends on the load cell, and a
// new one takes effect on the next sample.
void StateMachine::onSetFailsafe(const Command& command) {
    if (command.payloadSize < 6) {
        report(Cmd::ESP_error, "Failsafe refused: %u bytes, 6 expected", (unsigned)command.payloadSize);
        return;
    }
    const int32_t minCounts = readInt24BE(command.payload);
    const int32_t maxCounts = readInt24BE(command.payload + 3);
    if (minCounts >= maxCounts) {
        report(Cmd::ESP_error, "Failsafe refused: min %ld >= max %ld", (long)minCounts, (long)maxCounts);
        return;
    }
    forceMin_ = minCounts;
    forceMax_ = maxCounts;
    report(Cmd::ESP_debug, "Failsafe: force %ld to %ld counts", (long)forceMin_, (long)forceMax_);
}

void StateMachine::onSequenceStep(const Command& command) {
    if (state_ != State::Armed) {
        report(Cmd::ESP_error, "Sequence step ignored: the machine is %s", stateName(state_));
        return;
    }
    uint8_t order;
    SequenceStep step;
    if (!parseSequenceStep(command.payload, command.payloadSize, order, step) || order >= stepsExpected_) {
        denyRun("Run refused: bad sequence step");
        return;
    }
    steps_[order] = step;
    if (!stepReceived_[order]) {
        stepReceived_[order] = true;
        stepsReceived_++;
    }
}

void StateMachine::onRunStart(uint32_t nowMs) {
    if (state_ != State::Armed) {
        report(Cmd::ESP_error, "Run start ignored: the machine is %s", stateName(state_));
        return;
    }
    if (stepsReceived_ != stepsExpected_) {
        denyRun("Run refused: sequence incomplete, %u of %u steps", (unsigned)stepsReceived_,
                (unsigned)stepsExpected_);
        return;
    }
    if (!strokeKnown_) {
        denyRun("Run refused: the stroke isn't known (calibrate or TAR first)");
        return;
    }
    if (!sensorAlive(nowMs)) {
        denyRun("Run refused: no data from the sensors");
        return;
    }
    startRun(nowMs);
}

// A refused TAR is answered with ESP_run_denied too: the GUI waits in its Tar
// state for either that or the ESP_tar_data at the end of the TAR.
void StateMachine::onTarRequest(bool measureEmptyLoad, uint32_t nowMs) {
    const char* what = measureEmptyLoad ? "TAR" : "Bottom out";
    if (state_ != State::Idle) {
        report(Cmd::ESP_error, "%s refused: the machine is %s", what, stateName(state_));
        gui_->send(Cmd::ESP_run_denied);
        return;
    }
    if (refuseIfSafetyOpen(what)) {
        gui_->send(Cmd::ESP_run_denied);
        return;
    }
    if (!sensorAlive(nowMs)) {
        report(Cmd::ESP_error, "%s refused: no data from the sensors", what);
        gui_->send(Cmd::ESP_run_denied);
        return;
    }
    stopMotor(); // nothing turns while the empty load is measured
    enterMotionState(State::Tar, nowMs);
    tarMeasuresEmptyLoad_ = measureEmptyLoad;
    if (measureEmptyLoad) {
        report(Cmd::ESP_message, "TAR: measuring the empty load (the bench must be empty)");
        restForce_.start(nowMs, TAR_SETTLE_MS, TAR_EMPTY_LOAD_SAMPLES);
        tarPhase_ = TarPhase::EmptyLoad;
    } else {
        report(Cmd::ESP_message, "Bottom out: looking for the stroke");
        startTarMotion(nowMs);
    }
}

void StateMachine::onDebugDriveFrame(const Command& command) {
    if (state_ != State::Idle) {
        report(Cmd::ESP_error, "Drive frame refused: the machine is %s", stateName(state_));
        return;
    }
    if (refuseIfSafetyOpen("Drive frame")) {
        return;
    }
    if (!drive_->sendFrame(command.payload, command.payloadSize)) {
        report(Cmd::ESP_error, "Bad drive frame");
    }
}

// The GUI closed, crashed, froze or was unplugged: nobody is left to stop the motor, so
// stop it. A Panic stays as it is: the motor is already stopped, and the GUI hears about
// it when it connects again. The Idle case stops the motor too: the debug button can
// run it from there.
void StateMachine::onGuiLost(uint32_t nowMs) {
    guiLinked_ = false;
    const unsigned long silentMs = (unsigned long)(nowMs - guiLastMs_);
    const State was = state_;
    if (was == State::Panic) {
        report(Cmd::ESP_debug, "GUI silent for %lu ms: link dropped", silentMs);
        return;
    }
    abort(); // motor stopped, Idle; the points of a run so far are still sent
    if (was == State::Idle) {
        report(Cmd::ESP_debug, "GUI silent for %lu ms: link dropped", silentMs);
    } else {
        report(Cmd::ESP_error, "GUI silent for %lu ms: %s stopped", silentMs, stateName(was));
    }
}

void StateMachine::onReboot() {
    stopMotor();
    // No keep-alive while the ESP restarts: turn the timeout off so the drive
    // doesn't go to CE (which only the reset button clears). The boot turns
    // it back on.
    if (!drive_->setCommTimeout(0)) {
        report(Cmd::ESP_error, "Drive timeout not turned off: expect a CE fault");
    }
    report(Cmd::ESP_message, "Rebooting");
    rebootRequested_ = true;
}

// =============================================================================
// Per sample
// =============================================================================

// Both checks use the median-filtered values and must hold for several
// samples in a row, so a single bad reading can't stop the machine.
bool StateMachine::safetyChecksPass() {
    if (railInARow_.update(atRail(position_) || atRail(force_), SAFETY_CONSECUTIVE_SAMPLES)) {
        enterPanic(FaultReason::SensorOutOfRange, "position %ld, force %ld", (long)position_, (long)force_);
        return false;
    }
    if (FORCE_FAILSAFE_ENABLED) {
        // raw reading, not relative to the TAR (see DEFAULT_FORCE_LIMIT_COUNTS)
        const bool over = force_ > forceMax_ || force_ < forceMin_;
        if (overloadInARow_.update(over, SAFETY_CONSECUTIVE_SAMPLES)) {
            enterPanic(FaultReason::ForceFailsafe, "force %ld counts, window %ld to %ld",
                       (long)force_, (long)forceMin_, (long)forceMax_);
            return false;
        }
    }
    return true;
}

// Run calibration, with the shock mounted (so the yoke starts near the bottom):
// the stroke from one cycle at CALIBRATION_CENTIHZ, then, at the slower
// CALIBRATION_FIT_CENTIHZ, one cycle for the static force line. See config.h.
void StateMachine::updateCalibration(uint32_t nowMs) {
    if (nowMs - calibrationMotionStartMs_ >= CALIBRATION_TIMEOUT_MS) {
        enterPanic(FaultReason::StrokeNotFound, "calibration not done within %lu s",
                   (unsigned long)(CALIBRATION_TIMEOUT_MS / 1000));
        return;
    }
    switch (calibrationPhase_) {
        case CalibrationPhase::FindStroke:
            turnFinder_.update(position_);
            if (!turnFinder_.found()) {
                return;
            }
            setStroke(turnFinder_.min(), turnFinder_.max());
            if (!drive_->setFrequency(CALIBRATION_FIT_CENTIHZ)) {
                enterPanic(FaultReason::DriveFault, "drive command not sent");
                return;
            }
            drive_->forgetRunningFrequency(); // the speed the drive reported before doesn't count
            lastQueryMs_ = nowMs - DRIVE_QUERY_PERIOD_MS; // ask right away
            startPhaseClock(nowMs);
            calibrationPhase_ = CalibrationPhase::SlowDown;
            report(Cmd::ESP_debug, "Calibration: stroke %ld to %ld, slowing to %u.%02u Hz", (long)strokeMin_,
                   (long)strokeMax_, (unsigned)(CALIBRATION_FIT_CENTIHZ / 100), (unsigned)(CALIBRATION_FIT_CENTIHZ % 100));
            return;

        case CalibrationPhase::SlowDown:
            switch (checkSpeed(CALIBRATION_FIT_CENTIHZ, nowMs)) {
                case SpeedCheck::Waiting:
                    return;
                case SpeedCheck::TimedOut:
                    enterPanic(FaultReason::DriveFault, "didn't slow to %u.%02u Hz", (unsigned)(CALIBRATION_FIT_CENTIHZ / 100),
                               (unsigned)(CALIBRATION_FIT_CENTIHZ % 100));
                    return;
                case SpeedCheck::Reached:
                    break;
            }
            staticFit_.start(strokeMin_, strokeMax_, BOTTOM_AT_HIGH_POSITION, CALIBRATION_END_WINDOW_PERMILLE,
                             STROKE_TURN_HYSTERESIS_COUNTS);
            calibrationPhase_ = CalibrationPhase::Fit;
            report(Cmd::ESP_debug, "Calibration: measuring the static force");
            return;

        case CalibrationPhase::Fit:
            if (!staticFit_.update(position_, force_)) {
                return;
            }
            bottomForce_ = staticFit_.bottomLine();
            topForce_ = staticFit_.topLine();
            finishCalibration(nowMs);
            return;
    }
}

void StateMachine::updateOperating(const SensorSample& sample, uint32_t nowMs) {
    if (runPhase_ == RunPhase::BottomOut) { // the run's points are all out
        if (updateBottomOut(nowMs) == Progress::Done) {
            finishRun();
        }
        return;
    }
    if (!runClockStarted_) {
        runStartUs_ = sample.timeUs;
        runClockStarted_ = true;
    }
    const uint32_t runUs = sample.timeUs - runStartUs_;

    Point point;
    point.position = sample.position; // raw values to the GUI, not the filtered ones
    point.force = sample.force;
    point.timeUs = runUs;
    points_->push(point);

    const SequenceStep& step = steps_[stepIndex_];
    switch (runPhase_) {
        case RunPhase::SpeedingUp:
            switch (checkSpeed(step.targetCentiHz, nowMs)) {
                case SpeedCheck::Waiting:
                    return;
                case SpeedCheck::TimedOut:
                    enterPanic(FaultReason::DriveFault, "didn't reach %u.%02u Hz",
                               (unsigned)(step.targetCentiHz / 100), (unsigned)(step.targetCentiHz % 100));
                    return;
                case SpeedCheck::Reached:
                    break;
            }
            // The step's section starts with its first cycle (below); until
            // then, from here.
            stepStartUs_ = runUs;
            cycleCounter_.start(strokeMin_, strokeMax_, CYCLE_LOW_PERCENT, CYCLE_HIGH_PERCENT);
            cyclesDone_ = 0;
            lastCycleMs_ = nowMs;
            runPhase_ = RunPhase::Counting;
            report(Cmd::ESP_debug, "Step %u at speed, counting %u cycles", (unsigned)(stepIndex_ + 1),
                   (unsigned)step.cycles);
            return;

        case RunPhase::Counting: {
            const bool started = cycleCounter_.started();
            const bool completed = cycleCounter_.update(position_);
            if (!started && cycleCounter_.started()) {
                stepStartUs_ = runUs; // the section holds exactly the counted cycles
            }
            if (completed) {
                cyclesDone_++;
                lastCycleMs_ = nowMs;
                report(Cmd::ESP_debug, "Step %u: cycle %u of %u", (unsigned)(stepIndex_ + 1),
                       (unsigned)cyclesDone_, (unsigned)step.cycles);
            }
            if (cyclesDone_ >= step.cycles) {
                finishStep(runUs, nowMs);
            } else if (nowMs - lastCycleMs_ >= CYCLE_TIMEOUT_MS) {
                enterPanic(FaultReason::CycleTimeout, "step %u, %u of %u cycles", (unsigned)(stepIndex_ + 1),
                           (unsigned)cyclesDone_, (unsigned)step.cycles);
            }
            return;
        }

        case RunPhase::BottomOut:
            return; // handled above
    }
}

void StateMachine::updateTar(uint32_t nowMs) {
    switch (tarPhase_) {
        case TarPhase::EmptyLoad:
            if (!restForce_.update(force_, nowMs)) {
                return;
            }
            emptyLoad_ = restForce_.value();
            emptyLoadKnown_ = true;
            report(Cmd::ESP_message, "TAR: empty load %ld counts (~%ld N), looking for the stroke",
                   (long)emptyLoad_, newtons(emptyLoad_));
            startTarMotion(nowMs);
            return;

        case TarPhase::FindStroke:
            strokeFinder_.update(position_, nowMs);
            switch (strokeFinder_.result(nowMs)) {
                case StrokeFinder::Result::Searching:
                    return;
                case StrokeFinder::Result::Failed:
                    enterPanic(FaultReason::StrokeNotFound, "range only %ld counts",
                               (long)strokeFinder_.range());
                    return;
                case StrokeFinder::Result::Found:
                    break;
            }
            setStroke(strokeFinder_.min(), strokeFinder_.max());
            report(Cmd::ESP_debug, "TAR: stroke %ld to %ld, going to the bottom", (long)strokeMin_,
                   (long)strokeMax_);
            if (startBottomOut(nowMs)) {
                tarPhase_ = TarPhase::BottomOut;
            }
            return;

        case TarPhase::BottomOut:
            if (updateBottomOut(nowMs) == Progress::Done) {
                finishTar();
            }
            return;
    }
}

// =============================================================================
// Run
// =============================================================================

void StateMachine::startRun(uint32_t nowMs) {
    stepIndex_ = 0;
    runClockStarted_ = false; // starts with the first sample in updateOperating()
    points_->reset();         // nothing of a previous run gets sent with this one
    if (!beginStep(nowMs)) {
        denyRun("Run refused: drive command not sent");
        return;
    }
    enterMotionState(State::Operating, nowMs);
    gui_->send(Cmd::ESP_run_starting);
}

// Sends the drive the step's frequency (and the start command before the
// first step), then waits for it to get there.
bool StateMachine::beginStep(uint32_t nowMs) {
    const SequenceStep& step = steps_[stepIndex_];
    bool sent = drive_->sendFrame(step.driveFrame, sizeof(step.driveFrame));
    if (sent && stepIndex_ == 0) {
        sent = drive_->runForward();
    }
    if (!sent) {
        return false;
    }
    runPhase_ = RunPhase::SpeedingUp;
    startPhaseClock(nowMs);
    drive_->forgetRunningFrequency();
    report(Cmd::ESP_debug, "Step %u of %u: %u.%02u Hz, %u cycles", (unsigned)(stepIndex_ + 1),
           (unsigned)stepsExpected_, (unsigned)(step.targetCentiHz / 100), (unsigned)(step.targetCentiHz % 100),
           (unsigned)step.cycles);
    return true;
}

// The step has its cycles: tell the GUI where its section starts and ends,
// then go to the next speed, or end the run after the last step.
void StateMachine::finishStep(uint32_t endUs, uint32_t nowMs) {
    uint8_t payload[8];
    writeUint32BE(stepStartUs_, payload);
    writeUint32BE(endUs, payload + 4);
    gui_->send(Cmd::ESP_sequence_separator, payload, sizeof(payload));

    stepIndex_++;
    if (stepIndex_ < stepsExpected_) {
        if (!beginStep(nowMs)) {
            enterPanic(FaultReason::DriveFault, "drive command not sent");
        }
        return;
    }

    // Every step done: the run's points end here. Park the yoke at the bottom
    // before telling the GUI the run is over (finishRun).
    const uint32_t dropped = points_->dropped();
    if (dropped > 0) {
        report(Cmd::ESP_error, "%lu points were dropped (link too slow)", (unsigned long)dropped);
    }
    gui_->flushPoints();
    if (startBottomOut(nowMs)) {
        runPhase_ = RunPhase::BottomOut;
    }
}

void StateMachine::finishRun() {
    report(Cmd::ESP_debug, "Run done, bottomed out at %.1f%% of the stroke", bottomPercent());
    goIdle();
    // After the last points (flushPoints keeps the order), so the GUI has them
    // all when it processes the run.
    gui_->flushPoints(Cmd::ESP_run_finished);
}

// =============================================================================
// Calibration
// =============================================================================

void StateMachine::finishCalibration(uint32_t nowMs) {
    stopMotor();
    sendCalibrationData();
    report(Cmd::ESP_message, "Calibration done: stroke %ld to %ld, static force %ld at the bottom, %ld at the top "
           "counts (%lu samples at the ends, %lu in the middle)", (long)strokeMin_, (long)strokeMax_, (long)bottomForce_,
           (long)topForce_, (unsigned long)staticFit_.endSamples(), (unsigned long)staticFit_.branchSamples());
    state_ = State::Armed;
    armedSinceMs_ = nowMs;
}

// =============================================================================
// TAR
// =============================================================================

bool StateMachine::startTarMotion(uint32_t nowMs) {
    if (!startMotor(TAR_CENTIHZ)) {
        return false;
    }
    startStrokeSearch(nowMs);
    tarPhase_ = TarPhase::FindStroke;
    return true;
}

// The report says where the yoke really stopped, to tune TAR_STOP_PERMILLE.
void StateMachine::finishTar() {
    const double stopSentPercent = TAR_STOP_PERMILLE / 10.0;
    if (tarMeasuresEmptyLoad_) {
        report(Cmd::ESP_message, "TAR done: empty load %ld counts, stopped at %.1f%% of the stroke "
               "(stop sent at %.1f%%, 100%% = bottom)", (long)emptyLoad_, bottomPercent(), stopSentPercent);
    } else {
        report(Cmd::ESP_message, "Bottom out done: stopped at %.1f%% of the stroke "
               "(stop sent at %.1f%%, 100%% = bottom)", bottomPercent(), stopSentPercent);
    }
    sendTarData(tarMeasuresEmptyLoad_ ? emptyLoad_ : forceAverage_.value());
    goIdle();
}

// =============================================================================
// Bottom out
// =============================================================================

// Parks the yoke at the bottom. First the motor slows to BOTTOM_OUT_CENTIHZ
// (from whatever it turns at: after a fast run that takes a while) and the drive
// has to REPORT that speed. Only then is the stop point armed: the stop is sent
// when the yoke passes TAR_STOP_PERMILLE on its way down, and the motor still
// turns a little while it decelerates, the less the slower it was turning. The
// stroke must be known and the motor running.
bool StateMachine::startBottomOut(uint32_t nowMs) {
    if (!drive_->setFrequency(BOTTOM_OUT_CENTIHZ)) {
        enterPanic(FaultReason::DriveFault, "drive command not sent");
        return false;
    }
    drive_->forgetRunningFrequency(); // the speed the drive reported before doesn't count
    lastQueryMs_ = nowMs - DRIVE_QUERY_PERIOD_MS; // ask right away
    startPhaseClock(nowMs);
    bottomOutPhase_ = BottomOutPhase::SlowDown;
    report(Cmd::ESP_debug, "Bottom out: slowing to %u.%02u Hz", (unsigned)(BOTTOM_OUT_CENTIHZ / 100),
           (unsigned)(BOTTOM_OUT_CENTIHZ % 100));
    return true;
}

StateMachine::Progress StateMachine::updateBottomOut(uint32_t nowMs) {
    switch (bottomOutPhase_) {
        case BottomOutPhase::SlowDown:
            switch (checkSpeed(BOTTOM_OUT_CENTIHZ, nowMs, BOTTOM_OUT_TOLERANCE_CENTIHZ, BOTTOM_OUT_ASSUMED_MS,
                               BOTTOM_OUT_TIMEOUT_MS)) {
                case SpeedCheck::Waiting:
                    return Progress::Busy;
                case SpeedCheck::TimedOut:
                    enterPanic(FaultReason::DriveFault, "didn't slow to %u.%02u Hz", (unsigned)(BOTTOM_OUT_CENTIHZ / 100),
                               (unsigned)(BOTTOM_OUT_CENTIHZ % 100));
                    return Progress::Failed;
                case SpeedCheck::Reached:
                    break;
            }
            startPhaseClock(nowMs);
            bottomOutPhase_ = BottomOutPhase::SlowSettle;
            return Progress::Busy;

        case BottomOutPhase::SlowSettle:
            if (nowMs - phaseStartMs_ < BOTTOM_OUT_SETTLE_MS) {
                return Progress::Busy;
            }
            report(Cmd::ESP_debug, "Bottom out: at %u.%02u Hz, looking for the stop point",
                   (unsigned)(BOTTOM_OUT_CENTIHZ / 100), (unsigned)(BOTTOM_OUT_CENTIHZ % 100));
            approach_.start(strokeMin_, strokeMax_, BOTTOM_AT_HIGH_POSITION, TAR_STOP_PERMILLE);
            startPhaseClock(nowMs);
            bottomOutPhase_ = BottomOutPhase::Seek;
            return Progress::Busy;

        case BottomOutPhase::Seek:
            if (approach_.update(position_)) {
                stopMotor();
                drive_->forgetRunningFrequency();
                startPhaseClock(nowMs);
                bottomOutPhase_ = BottomOutPhase::Stopping;
                return Progress::Busy;
            }
            if (nowMs - phaseStartMs_ >= TAR_SEEK_TIMEOUT_MS) {
                enterPanic(FaultReason::CycleTimeout, "never reached the bottom out stop point");
                return Progress::Failed;
            }
            return Progress::Busy;

        case BottomOutPhase::Stopping:
            switch (checkSpeed(0, nowMs)) {
                case SpeedCheck::Waiting:
                    return Progress::Busy;
                case SpeedCheck::TimedOut:
                    enterPanic(FaultReason::DriveFault, "didn't stop");
                    return Progress::Failed;
                case SpeedCheck::Reached:
                    startPhaseClock(nowMs);
                    bottomOutPhase_ = BottomOutPhase::Settle;
                    return Progress::Busy;
            }
            return Progress::Busy;

        case BottomOutPhase::Settle:
            return nowMs - phaseStartMs_ >= TAR_STOPPED_SETTLE_MS ? Progress::Done : Progress::Busy;
    }
    return Progress::Busy;
}

// Where the yoke is, in % of the stroke (100 = bottom).
double StateMachine::bottomPercent() const {
    return strokePermille(position_, strokeMin_, strokeMax_, BOTTOM_AT_HIGH_POSITION) / 10.0;
}

// =============================================================================
// Transitions
// =============================================================================

void StateMachine::stopMotor() {
    if (!drive_->stop()) {
        report(Cmd::ESP_error, "Drive stop not sent");
    }
}

void StateMachine::goIdle() {
    stopMotor();
    state_ = State::Idle;
}

// GUI_abort and GUI_check_connection: stop everything, from any state. A
// Panic is acknowledged too, but only left once the safety circuit is
// closed: the drive can only be re-armed by the reset button on the machine.
void StateMachine::abort() {
    if (state_ == State::Operating) {
        gui_->flushPoints(); // the points up to the stop are worth seeing
    }
    if (state_ != State::Panic) {
        goIdle();
        return;
    }
    stopMotor();
    panicNeedsGui_ = false;
    if (safety_ == Safety::Closed) {
        leavePanic();
    } else {
        report(Cmd::ESP_error, "Still in panic: safety circuit open (press the reset button on the machine)");
    }
}

void StateMachine::leavePanic() {
    goIdle();
    report(Cmd::ESP_message, "Panic cleared");
    gui_->send(Cmd::ESP_emergency_clear);
}

// Boot and GUI connection: the drive's RS-485 timeout. The drive powers up
// with the value in its EEPROM, and this one is written to its RAM only.
void StateMachine::prepareDrive() {
    if (!drive_->setCommTimeout(DRIVE_COMM_TIMEOUT_DECISECONDS)) {
        report(Cmd::ESP_error, "Drive timeout not set");
    }
}

// The drive's ready relay, debounced: open for SAFETY_TRIP_MS opens the
// circuit (Panic), closed for SAFETY_CLEAR_MS closes it. A Panic caused by the
// circuit clears when it closes: the reset button is the acknowledgement.
void StateMachine::onSafetyInput(bool ready, uint32_t nowMs) {
    if (ready != safetyRaw_) {
        safetyRaw_ = ready;
        safetyRawSinceMs_ = nowMs;
    }
    const uint32_t steadyMs = nowMs - safetyRawSinceMs_;
    if (!ready && steadyMs >= SAFETY_TRIP_MS && safety_ != Safety::Open) {
        safety_ = Safety::Open;
        if (state_ == State::Panic) {
            panicNeedsGui_ = false; // the reset button will acknowledge both
            panicReason_ = FaultReason::SafetyCircuit;
            report(Cmd::ESP_error, "Safety circuit opened (e-stop, door or drive fault)");
        } else {
            enterPanic(FaultReason::SafetyCircuit, "e-stop, door or drive fault");
        }
    } else if (ready && steadyMs >= SAFETY_CLEAR_MS && safety_ != Safety::Closed) {
        const bool wasOpen = safety_ == Safety::Open;
        safety_ = Safety::Closed;
        if (wasOpen) {
            report(Cmd::ESP_debug, "Safety circuit closed");
        }
        if (state_ == State::Panic && !panicNeedsGui_) {
            leavePanic();
        }
    }
}

bool StateMachine::refuseIfSafetyOpen(const char* what) {
    if (safety_ == Safety::Closed) {
        return false;
    }
    report(Cmd::ESP_error, "%s refused: safety circuit open (press the reset button on the machine)", what);
    return true;
}

void StateMachine::enterMotionState(State state, uint32_t nowMs) {
    state_ = state;
    startPhaseClock(nowMs);
    railInARow_.reset();
    overloadInARow_.reset();
}

// A run request, calibration or start that can't go on: the GUI is waiting
// in Requesting/Calibrating/Starting and goes back to Idle on ESP_run_denied.
void StateMachine::denyRun(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    gui_->sendv(Cmd::ESP_error, fmt, args);
    va_end(args);
    gui_->send(Cmd::ESP_run_denied);
    if (state_ == State::Armed || state_ == State::Calibration) {
        goIdle();
    }
}

void StateMachine::enterPanic(FaultReason reason, const char* fmt, ...) {
    if (state_ == State::Panic) {
        return;
    }
    const State was = state_;
    state_ = State::Panic;
    panicReason_ = reason;
    panicNeedsGui_ = reason != FaultReason::SafetyCircuit;
    stopMotor();

    char detail[96] = "";
    if (fmt != nullptr) {
        va_list args;
        va_start(args, fmt);
        vsnprintf(detail, sizeof(detail), fmt, args);
        va_end(args);
    }
    if (detail[0] != '\0') {
        report(Cmd::ESP_error, "PANIC: %s (%s)", faultName(reason), detail);
    } else {
        report(Cmd::ESP_error, "PANIC: %s", faultName(reason));
    }
    gui_->send(Cmd::ESP_emergency);
    if (was == State::Calibration) {
        gui_->send(Cmd::ESP_run_denied); // the GUI is still waiting for the run to start
    }
    if (was == State::Operating) {
        gui_->flushPoints(); // the points up to the fault are worth seeing
    }
}

// =============================================================================
// Helpers
// =============================================================================

// Asks the drive for its running frequency every DRIVE_QUERY_PERIOD_MS and
// compares the latest answer with the target. A drive that never answers is
// assumed to get there in SPEEDUP_ASSUMED_MS.
StateMachine::SpeedCheck StateMachine::checkSpeed(uint16_t targetCentiHz, uint32_t nowMs, int32_t toleranceCentiHz,
                                                  uint32_t assumedMs, uint32_t timeoutMs) {
    if (nowMs - lastQueryMs_ >= DRIVE_QUERY_PERIOD_MS) {
        lastQueryMs_ = nowMs;
        drive_->queryRunningFrequency(); // refused = queue full, tried again next period
    }
    const uint32_t waited = nowMs - phaseStartMs_;
    uint16_t centiHz;
    if (drive_->runningFrequency(centiHz)) {
        const int32_t diff = (int32_t)centiHz - (int32_t)targetCentiHz;
        if (diff <= toleranceCentiHz && diff >= -toleranceCentiHz) {
            return SpeedCheck::Reached;
        }
    } else if (waited >= assumedMs) {
        return SpeedCheck::Reached;
    }
    return waited >= timeoutMs ? SpeedCheck::TimedOut : SpeedCheck::Waiting;
}

void StateMachine::startPhaseClock(uint32_t nowMs) {
    phaseStartMs_ = nowMs;
    lastQueryMs_ = nowMs;
}

void StateMachine::startStrokeSearch(uint32_t nowMs) {
    strokeFinder_.start(nowMs, STROKE_MIN_RANGE_COUNTS, STROKE_MIN_TIME_MS, STROKE_SETTLE_MS, STROKE_TIMEOUT_MS);
}

void StateMachine::setStroke(int32_t strokeMin, int32_t strokeMax) {
    strokeMin_ = strokeMin;
    strokeMax_ = strokeMax;
    strokeKnown_ = true;
}

// Sets the frequency and starts the motor (forward). Panic if the drive
// commands can't be queued.
bool StateMachine::startMotor(uint16_t centiHz) {
    if (!drive_->setFrequency(centiHz) || !drive_->runForward()) {
        enterPanic(FaultReason::DriveFault, "drive command not sent");
        return false;
    }
    return true;
}

// ESP_calibration_data: [stroke min][stroke max][static force at the bottom][static force at the top], 24-bit raw counts.
void StateMachine::sendCalibrationData() {
    uint8_t payload[12];
    writeInt24BE(strokeMin_, payload);
    writeInt24BE(strokeMax_, payload + 3);
    writeInt24BE(clampInt24(bottomForce_), payload + 6);
    writeInt24BE(clampInt24(topForce_), payload + 9);
    gui_->send(Cmd::ESP_calibration_data, payload, sizeof(payload));
}

// ESP_tar_data: [stroke min][stroke max][force], 24-bit raw counts.
void StateMachine::sendTarData(int32_t force) {
    uint8_t payload[9];
    writeInt24BE(strokeMin_, payload);
    writeInt24BE(strokeMax_, payload + 3);
    writeInt24BE(clampInt24(force), payload + 6);
    gui_->send(Cmd::ESP_tar_data, payload, sizeof(payload));
}

// ESP_live_data: [position][stroke min][stroke max][force][temperature],
// 24-bit: raw counts, temperature in 0.01 degC. Zeros for what isn't known:
// no stroke before a TAR or a calibration, no sensor values while the ADC
// isn't delivering, no valid temperature read for a second.
void StateMachine::sendLiveData(uint32_t nowMs) {
    const bool sensors = sensorAlive(nowMs);
    uint8_t payload[15];
    writeInt24BE(sensors ? position_ : 0, payload);
    writeInt24BE(strokeKnown_ ? strokeMin_ : 0, payload + 3);
    writeInt24BE(strokeKnown_ ? strokeMax_ : 0, payload + 6);
    writeInt24BE(sensors ? clampInt24(forceAverage_.value()) : 0, payload + 9);
    writeInt24BE(clampInt24(temperature_), payload + 12);
    gui_->send(Cmd::ESP_live_data, payload, sizeof(payload));
}

void StateMachine::report(uint8_t flag, const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    gui_->sendv(flag, fmt, args);
    va_end(args);
}

bool StateMachine::inMotionState() const {
    return state_ == State::Calibration || state_ == State::Operating || state_ == State::Tar;
}

bool StateMachine::sensorAlive(uint32_t nowMs) const {
    return haveSample_ && nowMs - lastSampleMs_ <= SENSOR_STALL_MS;
}

