#include <Arduino.h>
#include <stdio.h>
#include <string.h>

#include "state_machine.h"
#include "comms_task.h"
#include "drive_comms.h"

namespace {

// Modbus frames for the drive (slave 0x03, function 0x06 = write one
// register), CRC included. DriveComms::sendFrame re-checks the CRC, so a typo
// here gets rejected instead of sent.
//
// Stop: register 0x2000 (control command) = 0x0005 (stop). Tested on the drive.
const uint8_t DRIVE_STOP_FRAME[] = {0x03, 0x06, 0x20, 0x00, 0x00, 0x05, 0x43, 0xEB};
// Start: register 0x2000 = 0x0001 (forward run). Tested on the drive.
const uint8_t DRIVE_START_FRAME[] = {0x03, 0x06, 0x20, 0x00, 0x00, 0x01, 0x42, 0x28};
// Speed 0: register 0x800A = P00.10 (set frequency, 0.01 Hz) = 0. The 0x8000
// bit makes the write go to the drive's RAM only, so it isn't saved to EEPROM
// (TD350 manual 9.4.5.1).
const uint8_t DRIVE_SPEED_ZERO_FRAME[] = {0x03, 0x06, 0x80, 0x0A, 0x00, 0x00, 0x81, 0xEA};

// Waiting for the drive to reach a step's frequency: it is asked for its
// running frequency (register 0x3000) this often, and the step starts counting
// once the answer is within the tolerance of the target.
constexpr uint32_t DRIVE_QUERY_PERIOD_MS = 200;
constexpr int32_t SPEED_TOLERANCE_CENTIHZ = 50; // 0.50 Hz

// If the drive never answers the frequency query, assume this is how long it
// takes to speed up.
constexpr uint32_t SPEEDUP_ASSUMED_MS = 3000;
// If it answers but never gets to the speed, give up after this long.
constexpr uint32_t SPEEDUP_TIMEOUT_MS = 30000;
// While counting, give up if no cycle completes for this long (nothing is
// moving, or the position sensor isn't working).
constexpr uint32_t CYCLE_TIMEOUT_MS = 30000;

void writeUint32BE(uint32_t v, uint8_t* out) {
    out[0] = (uint8_t)(v >> 24);
    out[1] = (uint8_t)(v >> 16);
    out[2] = (uint8_t)(v >> 8);
    out[3] = (uint8_t)v;
}

const char* faultName(FaultReason reason) {
    switch (reason) {
        case FaultReason::SensorStalled:    return "sensor stalled";
        case FaultReason::SensorOutOfRange: return "sensor out of range";
        case FaultReason::DriveFault:       return "drive fault";
        case FaultReason::LoopTimingDrift:  return "loop timing";
        case FaultReason::CycleTimeout:     return "no cycles detected";
        default:                            return "unknown";
    }
}

} // namespace

void StateMachine::begin(MotorControl* motor, DoubleBuffer* dataBuffer, CommsTask* comms, DriveComms* drive) {
    motor_ = motor;
    dataBuffer_ = dataBuffer;
    comms_ = comms;
    drive_ = drive;
    state_ = State::Idle;
}

void StateMachine::setPositionRange(int32_t min, int32_t max) {
    posMin_ = min;
    posMax_ = max;
}

bool StateMachine::sendDriveFrame(const uint8_t* frame, size_t length) {
    const bool ok = drive_ && drive_->sendFrame(frame, length);
    if (ok && length >= 6) {
        // Remember the last two frames (function, register, value) so a drive
        // error reply, which doesn't say which request it answers, can be
        // reported with the likely culprits.
        recentFrames_[1] = recentFrames_[0];
        recentFrames_[0].function = frame[1];
        recentFrames_[0].reg = (uint16_t)((frame[2] << 8) | frame[3]);
        recentFrames_[0].value = (uint16_t)((frame[4] << 8) | frame[5]);
        recentFrames_[0].valid = true;
    }
    return ok;
}

void StateMachine::stopDrive() {
    if (!drive_) {
        return;
    }
    // Stop first (urgent: replaces anything still queued for the drive),
    // then zero the speed so the next start doesn't resume at the old one.
    // DriveComms spaces the two frames apart.
    bool ok = drive_->sendFrame(DRIVE_STOP_FRAME, sizeof(DRIVE_STOP_FRAME), true);
    ok = drive_->sendFrame(DRIVE_SPEED_ZERO_FRAME, sizeof(DRIVE_SPEED_ZERO_FRAME)) && ok;
    if (!ok) {
        sendError("Drive stop not sent");
    }
}

bool StateMachine::sendCommand(uint8_t flag) {
    if (!comms_) {
        return false;
    }
    // TODO: a false return means the ack was dropped (priority queue full)
    // and the GUI will sit waiting for it; decide on a retry/panic policy.
    return comms_->enqueuePriorityMessage(flag, nullptr, 0);
}

void StateMachine::sendError(const char* text) {
    if (comms_) {
        comms_->enqueuePriorityMessage(Cmd::ESP_error, (const uint8_t*)text, strlen(text));
    }
}

void StateMachine::update(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs) {
    switch (state_) {
        case State::Idle:
            onIdle();
            break;
        case State::Armed:
            // Accepted a run request, waiting for the sequence and GUI_run_start.
            // TODO: timeout back to Idle if the start never arrives.
            break;
        case State::Calibration:
            onCalibration(positionRaw, forceRaw, timeMs);
            break;
        case State::Operating:
            onOperating(positionRaw, forceRaw, timeMs);
            break;
        case State::Panic:
            onPanic();
            break;
    }
}

void StateMachine::onIdle() {
    // No motion, no acquisition. TODO: watch instruction queue (handled
    // via handleInstruction) for "start calibration" / "start test".
}

void StateMachine::onCalibration(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs) {
    // TODO: seek -> slow approach -> stop-near-bottom sequence (design doc
    // open item 4, deferred until drive behavior is characterized). Likely
    // still wants to push Points to dataBuffer_ during this so the
    // calibration run is visible on the PC side -- confirm.
    (void)positionRaw;
    (void)forceRaw;
    (void)timeMs;
}

void StateMachine::onOperating(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs) {
    // Point times (and the separator timestamps) count microseconds from the
    // first sample of the run, so they stay small and the GUI can compare them.
    if (!runClockStarted_) {
        runStartUs_ = timeMs;
        runClockStarted_ = true;
    }
    uint32_t runTimeUs = timeMs - runStartUs_;

    Point p;
    p.position = positionRaw;
    p.force = forceRaw;
    p.time_ms = runTimeUs;
    if (dataBuffer_) dataBuffer_->push(p);

    switch (runPhase_) {
        case RunPhase::SpeedingUp:
            updateSpeedingUp(runTimeUs);
            break;
        case RunPhase::Counting:
            updateCounting(positionRaw, runTimeUs);
            break;
    }
}

// --- Run sequence -------------------------------------------------------

void StateMachine::startRun() {
    stepIndex_ = 0;
    runClockStarted_ = false; // the clock starts with the first sample in onOperating()
    if (dataBuffer_) {
        dataBuffer_->reset(); // nothing of the previous run gets sent into this one
    }
    state_ = State::Operating;

    if (!beginStep()) {
        return; // reported the error and went back to Idle
    }
    sendCommand(Cmd::ESP_run_starting);
}

// Sends the drive the frequency of the current step (and the start command
// before the first one), then waits for it to get there.
bool StateMachine::beginStep() {
    const SequenceStep& step = steps_[stepIndex_];

    bool ok = sendDriveFrame(step.driveFrame, sizeof(step.driveFrame));
    if (ok && stepIndex_ == 0) {
        ok = sendDriveFrame(DRIVE_START_FRAME, sizeof(DRIVE_START_FRAME));
    }
    if (!ok) {
        sendError("Drive command not sent");
        enterIdle();
        return false;
    }

    runPhase_ = RunPhase::SpeedingUp;
    phaseStartMs_ = millis();
    lastQueryMs_ = phaseStartMs_;
    driveFreqFresh_ = false; // only an answer received from now on counts
    return true;
}

void StateMachine::updateSpeedingUp(uint32_t runTimeUs) {
    const uint32_t now = millis();
    const uint32_t waited = now - phaseStartMs_;

    // Ask the drive how fast it is running. The answer arrives later through
    // handleDriveResponse(). A refused request (queue momentarily full) is
    // simply retried at the next period.
    if (now - lastQueryMs_ >= DRIVE_QUERY_PERIOD_MS) {
        lastQueryMs_ = now;
        uint8_t request[DRIVE_READ_REQUEST_SIZE];
        buildReadRequest(DRIVE_ADDRESS, DRIVE_REG_RUNNING_FREQ, 1, request);
        sendDriveFrame(request, sizeof(request));
    }

    bool reached = false;
    if (driveFreqFresh_) {
        int32_t diff = (int32_t)driveFreqCentiHz_ - (int32_t)steps_[stepIndex_].targetCentiHz;
        if (diff < 0) diff = -diff;
        reached = diff <= SPEED_TOLERANCE_CENTIHZ;
    } else if (waited >= SPEEDUP_ASSUMED_MS) {
        reached = true; // the drive doesn't answer: assume it took this long
    }

    if (!reached) {
        if (waited >= SPEEDUP_TIMEOUT_MS) {
            sendError("Drive did not reach the speed");
            raiseFault(FaultReason::DriveFault);
        }
        return;
    }

    // Speed reached: this is where the step's section of the run starts
    stepStartUs_ = runTimeUs;
    cycleCounter_.reset();
    cyclesDone_ = 0;
    lastCycleMs_ = now;
    runPhase_ = RunPhase::Counting;
}

void StateMachine::updateCounting(int32_t positionRaw, uint32_t runTimeUs) {
    const uint32_t now = millis();

    if (cycleCounter_.update(positionRaw, posMin_, posMax_)) {
        cyclesDone_++;
        lastCycleMs_ = now;
    }

    if (cyclesDone_ >= steps_[stepIndex_].cycles) {
        finishStep(runTimeUs);
    } else if (now - lastCycleMs_ >= CYCLE_TIMEOUT_MS) {
        sendError("No cycles detected");
        raiseFault(FaultReason::CycleTimeout);
    }
}

// The step has its cycles: tell the GUI where its section starts and ends,
// then climb to the next speed, or end the run after the last step.
void StateMachine::finishStep(uint32_t endUs) {
    uint8_t payload[8];
    writeUint32BE(stepStartUs_, payload);
    writeUint32BE(endUs, payload + 4);
    if (comms_) {
        comms_->enqueuePriorityMessage(Cmd::ESP_sequence_separator, payload, sizeof(payload));
    }

    stepIndex_++;
    if (stepIndex_ < stepsExpected_) {
        beginStep(); // on failure it reports the error and goes Idle
    } else {
        sendCommand(Cmd::ESP_run_finished);
        enterIdle(); // stops the drive and sends the last points
    }
}

void StateMachine::handleDriveResponse(const DriveFrame& frame) {
    uint8_t functionCode, errorCode;
    if (parseExceptionResponse(frame.bytes, frame.length, DRIVE_ADDRESS, functionCode, errorCode)) {
        // e.g. "Drive error: func 0x06 code 0x03 | last sent: 0x06 reg 0x800A val 0x03E8 ; 0x06 reg 0x2000 val 0x0001"
        // (newest first; the refused request is one of them)
        char msg[128];
        int n = snprintf(msg, sizeof(msg), "Drive error: func 0x%02X code 0x%02X | last sent:", functionCode, errorCode);
        for (int i = 0; i < 2; i++) {
            if (recentFrames_[i].valid && n > 0 && n < (int)sizeof(msg)) {
                n += snprintf(msg + n, sizeof(msg) - n, "%s 0x%02X reg 0x%04X val 0x%04X",
                              i == 0 ? "" : " ;", recentFrames_[i].function,
                              recentFrames_[i].reg, recentFrames_[i].value);
            }
        }
        sendError(msg);
        if (state_ == State::Operating) {
            raiseFault(FaultReason::DriveFault);
        }
        return;
    }

    uint16_t value;
    if (parseReadResponseOneRegister(frame.bytes, frame.length, DRIVE_ADDRESS, value)) {
        driveFreqCentiHz_ = value;
        driveFreqFresh_ = true;
    }
    // Anything else (the drive echoing a write, ...) is ignored.
}

void StateMachine::onPanic() {
    // Nothing ongoing here -- entry into panic already stopped the motor
    // and attempted to notify the PC (see enterPanic()). Just wait for an
    // explicit "return to idle" instruction (handleInstruction) or a
    // hardware reset.
}

void StateMachine::enterPanic(FaultReason reason) {
    const bool wasOperating = (state_ == State::Operating);
    lastFault_ = reason;
    state_ = State::Panic;
    if (motor_) {
        motor_->stop();
    }
    stopDrive();

    char msg[40];
    snprintf(msg, sizeof(msg), "PANIC: %s", faultName(reason));
    sendError(msg);

    if (wasOperating && comms_) {
        comms_->requestFlush(); // the points up to the fault are worth seeing
    }
}

void StateMachine::enterIdle() {
    const bool wasOperating = (state_ == State::Operating);
    state_ = State::Idle;
    lastFault_ = FaultReason::None;
    if (motor_) {
        motor_->stop();
    }
    stopDrive();

    if (wasOperating && comms_) {
        comms_->requestFlush(); // send the last, partly filled, buffer of points
    }
}

void StateMachine::raiseFault(FaultReason reason) {
    if (state_ != State::Panic) {
        enterPanic(reason);
    }
}

void StateMachine::handleInstruction(const Instruction& instr) {
    // TODO: dispatch on instr.flag (see command_flags.h):
    //   - start calibration -> state_ = State::Calibration (from Idle)
    //   - stop/cancel        -> enterIdle() (from any non-Panic state)
    //   - return to idle     -> enterIdle() (only valid from Panic, per
    //                            design doc section 2 -- panic does not
    //                            auto-recover)

    // TODO: handle GUI_calibrate

    // State is changed before the reply is queued so the ESP is already in
    // the new state by the time the GUI reacts to the ack.
    switch (instr.flag) {
        case Cmd::GUI_check_connection:
            // Sent on initial connect. Reset to a known-safe state, then pong.
            enterIdle();
            sendCommand(Cmd::ESP_good_connection);
            break;
        case Cmd::GUI_run_request:
            if (state_ == State::Idle) {
                // Payload: number of GUI_send_sequence steps that will follow
                const uint8_t count = instr.payloadSize >= 1 ? instr.payload[0] : 0;
                if (count == 0 || count > MAX_SEQUENCE_STEPS) {
                    sendError("Run request refused: bad number of steps");
                    break;
                }
                // TODO: check the drive is ready before accepting.
                memset(stepReceived_, 0, sizeof(stepReceived_));
                stepsExpected_ = count;
                stepsReceived_ = 0;
                state_ = State::Armed;
                sendCommand(Cmd::ESP_run_accepted);
            }
            // TODO: reply with a rejection once one exists in the protocol.
            break;
        case Cmd::GUI_send_sequence:
            if (state_ == State::Armed) {
                uint8_t order;
                SequenceStep step;
                if (!parseSequenceStep(instr.payload, instr.payloadSize, order, step) ||
                    order >= stepsExpected_) {
                    sendError("Bad sequence step");
                    break;
                }
                steps_[order] = step;
                if (!stepReceived_[order]) {
                    stepReceived_[order] = true;
                    stepsReceived_++;
                }
            }
            break;
        case Cmd::GUI_run_start:
            if (state_ == State::Armed) {
                if (stepsReceived_ != stepsExpected_) {
                    char msg[48];
                    snprintf(msg, sizeof(msg), "Sequence incomplete: %u of %u steps",
                             (unsigned)stepsReceived_, (unsigned)stepsExpected_);
                    sendError(msg);
                    enterIdle();
                    break;
                }
                startRun();
            }
            break;
        case Cmd::GUI_abort:
            enterIdle(); // also stops the drive
            // TODO: ack with ESP_run_finished once the GUI has a Stopping state.
            break;
        default:
            break;
    }
}
