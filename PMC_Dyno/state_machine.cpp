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
    forceLimit_ = DEFAULT_FORCE_LIMIT_COUNTS;
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
            onCalibrate(command, nowMs);
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

void StateMachine::onCalibrate(const Command& command, uint32_t nowMs) {
    if (state_ == State::Idle) {
        // Debug button: report what is known, without moving anything.
        sendCalibrationData(forceAverage_.value());
        return;
    }
    if (state_ != State::Armed) {
        report(Cmd::ESP_error, "Calibration refused: the machine is %s", stateName(state_));
        return;
    }
    const int32_t limit = command.payloadSize >= 3 ? readInt24BE(command.payload) : 0;
    forceLimit_ = limit > 0 ? limit : DEFAULT_FORCE_LIMIT_COUNTS;
    report(Cmd::ESP_debug, "Calibration: force limit %ld counts (~%ld N)", (long)forceLimit_, newtons(forceLimit_));

    enterMotionState(State::Calibration, nowMs);
    calibrationPhase_ = CalibrationPhase::RestLoad;
    forceMean_.reset();
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

void StateMachine::onTarRequest(bool measureEmptyLoad, uint32_t nowMs) {
    const char* what = measureEmptyLoad ? "TAR" : "Bottom out";
    if (state_ != State::Idle) {
        report(Cmd::ESP_error, "%s refused: the machine is %s", what, stateName(state_));
        return;
    }
    if (refuseIfSafetyOpen(what)) {
        return;
    }
    if (!sensorAlive(nowMs)) {
        report(Cmd::ESP_error, "%s refused: no data from the sensors", what);
        return;
    }
    stopMotor(); // nothing turns while the empty load is measured
    enterMotionState(State::Tar, nowMs);
    tarMeasuresEmptyLoad_ = measureEmptyLoad;
    if (measureEmptyLoad) {
        report(Cmd::ESP_message, "TAR: measuring the empty load (the bench must be empty)");
        tarPhase_ = TarPhase::Settle;
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
        const int64_t load = (int64_t)force_ - (emptyLoadKnown_ ? emptyLoad_ : 0);
        const bool over = load > forceLimit_ || load < -(int64_t)forceLimit_;
        if (overloadInARow_.update(over, SAFETY_CONSECUTIVE_SAMPLES)) {
            enterPanic(FaultReason::ForceFailsafe, "%ld N, limit %ld N", newtons(load), newtons(forceLimit_));
            return false;
        }
    }
    return true;
}

void StateMachine::updateCalibration(uint32_t nowMs) {
    switch (calibrationPhase_) {
        case CalibrationPhase::RestLoad:
            forceMean_.add(force_);
            if (forceMean_.count() < CALIBRATION_REST_SAMPLES) {
                return;
            }
            restLoad_ = forceMean_.mean();
            if (!drive_->setFrequency(CALIBRATION_CENTIHZ) || !drive_->runForward()) {
                enterPanic(FaultReason::DriveFault, "drive command not sent");
                return;
            }
            startStrokeSearch(nowMs);
            calibrationPhase_ = CalibrationPhase::FindStroke;
            report(Cmd::ESP_debug, "Calibration: rest load %ld counts, looking for the stroke", (long)restLoad_);
            return;

        case CalibrationPhase::FindStroke:
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
            strokeMin_ = strokeFinder_.min();
            strokeMax_ = strokeFinder_.max();
            strokeKnown_ = true;
            stopMotor();
            sendCalibrationData(restLoad_);
            report(Cmd::ESP_message, "Calibration done: stroke %ld to %ld", (long)strokeMin_, (long)strokeMax_);
            state_ = State::Armed;
            armedSinceMs_ = nowMs;
            return;
    }
}

void StateMachine::updateOperating(const SensorSample& sample, uint32_t nowMs) {
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
            // This is where the step's section of the run starts.
            stepStartUs_ = runUs;
            cycleCounter_.start(strokeMin_, strokeMax_, CYCLE_LOW_PERCENT, CYCLE_HIGH_PERCENT);
            cyclesDone_ = 0;
            lastCycleMs_ = nowMs;
            runPhase_ = RunPhase::Counting;
            report(Cmd::ESP_debug, "Step %u at speed, counting %u cycles", (unsigned)(stepIndex_ + 1),
                   (unsigned)step.cycles);
            return;

        case RunPhase::Counting:
            if (cycleCounter_.update(position_)) {
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
}

void StateMachine::updateTar(uint32_t nowMs) {
    switch (tarPhase_) {
        case TarPhase::Settle:
            if (nowMs - phaseStartMs_ >= TAR_SETTLE_MS) {
                forceMean_.reset();
                tarPhase_ = TarPhase::EmptyLoad;
            }
            return;

        case TarPhase::EmptyLoad:
            forceMean_.add(force_);
            if (forceMean_.count() < TAR_EMPTY_LOAD_SAMPLES) {
                return;
            }
            emptyLoad_ = forceMean_.mean();
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
            strokeMin_ = strokeFinder_.min();
            strokeMax_ = strokeFinder_.max();
            strokeKnown_ = true;
            seekArmed_ = false;
            startPhaseClock(nowMs);
            tarPhase_ = TarPhase::SeekBottom;
            report(Cmd::ESP_debug, "TAR: stroke %ld to %ld, going to the bottom", (long)strokeMin_,
                   (long)strokeMax_);
            return;

        case TarPhase::SeekBottom: {
            // Stop on the way down, once past the stop point: the motor still
            // turns a little while it decelerates.
            const int32_t permille = bottomPermille(position_);
            if (!seekArmed_) {
                seekArmed_ = permille <= 500; // seen in the top half first
            } else if (permille >= TAR_STOP_PERMILLE) {
                stopMotor();
                drive_->forgetRunningFrequency();
                startPhaseClock(nowMs);
                tarPhase_ = TarPhase::Stopping;
                return;
            }
            if (nowMs - phaseStartMs_ >= TAR_SEEK_TIMEOUT_MS) {
                enterPanic(FaultReason::CycleTimeout, "never reached the stop point");
            }
            return;
        }

        case TarPhase::Stopping:
            switch (checkSpeed(0, nowMs)) {
                case SpeedCheck::Waiting:
                    return;
                case SpeedCheck::TimedOut:
                    enterPanic(FaultReason::DriveFault, "didn't stop");
                    return;
                case SpeedCheck::Reached:
                    startPhaseClock(nowMs);
                    tarPhase_ = TarPhase::StoppedSettle;
                    return;
            }
            return;

        case TarPhase::StoppedSettle:
            if (nowMs - phaseStartMs_ >= TAR_STOPPED_SETTLE_MS) {
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

    const uint32_t dropped = points_->dropped();
    goIdle();
    if (dropped > 0) {
        report(Cmd::ESP_error, "%lu points were dropped (link too slow)", (unsigned long)dropped);
    }
    // After the last points, so the GUI has them all when it processes the run.
    gui_->flushPoints(Cmd::ESP_run_finished);
}

// =============================================================================
// TAR
// =============================================================================

bool StateMachine::startTarMotion(uint32_t nowMs) {
    if (!drive_->setFrequency(TAR_CENTIHZ) || !drive_->runForward()) {
        enterPanic(FaultReason::DriveFault, "drive command not sent");
        return false;
    }
    startStrokeSearch(nowMs);
    tarPhase_ = TarPhase::FindStroke;
    return true;
}

// The report says where the yoke really stopped, to tune TAR_STOP_PERMILLE.
void StateMachine::finishTar() {
    const double stoppedPercent = bottomPermille(position_) / 10.0;
    const double stopSentPercent = TAR_STOP_PERMILLE / 10.0;
    if (tarMeasuresEmptyLoad_) {
        report(Cmd::ESP_message, "TAR done: empty load %ld counts, stopped at %.1f%% of the stroke "
               "(stop sent at %.1f%%, 100%% = bottom)", (long)emptyLoad_, stoppedPercent, stopSentPercent);
    } else {
        report(Cmd::ESP_message, "Bottom out done: stopped at %.1f%% of the stroke "
               "(stop sent at %.1f%%, 100%% = bottom)", stoppedPercent, stopSentPercent);
    }
    sendCalibrationData(emptyLoadKnown_ ? emptyLoad_ : forceAverage_.value());
    goIdle();
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

// The drive's ready relay, debounced: LOW for SAFETY_TRIP_MS opens the
// circuit (Panic), HIGH for SAFETY_CLEAR_MS closes it. A Panic caused by the
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
StateMachine::SpeedCheck StateMachine::checkSpeed(uint16_t targetCentiHz, uint32_t nowMs) {
    if (nowMs - lastQueryMs_ >= DRIVE_QUERY_PERIOD_MS) {
        lastQueryMs_ = nowMs;
        drive_->queryRunningFrequency(); // refused = queue full, tried again next period
    }
    const uint32_t waited = nowMs - phaseStartMs_;
    uint16_t centiHz;
    if (drive_->runningFrequency(centiHz)) {
        const int32_t diff = (int32_t)centiHz - (int32_t)targetCentiHz;
        if (diff <= SPEED_TOLERANCE_CENTIHZ && diff >= -SPEED_TOLERANCE_CENTIHZ) {
            return SpeedCheck::Reached;
        }
    } else if (waited >= SPEEDUP_ASSUMED_MS) {
        return SpeedCheck::Reached;
    }
    return waited >= SPEEDUP_TIMEOUT_MS ? SpeedCheck::TimedOut : SpeedCheck::Waiting;
}

void StateMachine::startPhaseClock(uint32_t nowMs) {
    phaseStartMs_ = nowMs;
    lastQueryMs_ = nowMs;
}

void StateMachine::startStrokeSearch(uint32_t nowMs) {
    strokeFinder_.start(nowMs, STROKE_MIN_RANGE_COUNTS, STROKE_MIN_TIME_MS, STROKE_SETTLE_MS, STROKE_TIMEOUT_MS);
}

// ESP_calibration_data: [stroke min][stroke max][force], 24-bit raw counts.
void StateMachine::sendCalibrationData(int32_t force) {
    uint8_t payload[9];
    writeInt24BE(strokeKnown_ ? strokeMin_ : 0, payload);
    writeInt24BE(strokeKnown_ ? strokeMax_ : 0, payload + 3);
    writeInt24BE(clampInt24(force), payload + 6);
    gui_->send(Cmd::ESP_calibration_data, payload, sizeof(payload));
}

// ESP_live_data: [position][stroke min][stroke max][force][temperature],
// 24-bit raw counts. Zeros for what isn't known: no stroke before a TAR or a
// calibration, no sensor values while the ADC isn't delivering.
void StateMachine::sendLiveData(uint32_t nowMs) {
    const bool sensors = sensorAlive(nowMs);
    const int32_t temperature = 0; // TODO: no temperature sensor yet
    uint8_t payload[15];
    writeInt24BE(sensors ? position_ : 0, payload);
    writeInt24BE(strokeKnown_ ? strokeMin_ : 0, payload + 3);
    writeInt24BE(strokeKnown_ ? strokeMax_ : 0, payload + 6);
    writeInt24BE(sensors ? clampInt24(forceAverage_.value()) : 0, payload + 9);
    writeInt24BE(temperature, payload + 12);
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

// Where 'position' is along the stroke, in 0.1 %: 0 = top, 1000 = bottom.
int32_t StateMachine::bottomPermille(int32_t position) const {
    const int64_t span = (int64_t)strokeMax_ - strokeMin_;
    if (span <= 0) {
        return 0;
    }
    const int32_t fromMin = (int32_t)(((int64_t)position - strokeMin_) * 1000 / span);
    return BOTTOM_AT_HIGH_POSITION ? fromMin : 1000 - fromMin;
}
