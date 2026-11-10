#include <Arduino.h>

#include "state_machine.h"
#include "comms_task.h"

void StateMachine::begin(MotorControl* motor, DoubleBuffer* dataBuffer, CommsTask* comms) {
    motor_ = motor;
    dataBuffer_ = dataBuffer;
    comms_ = comms;
    state_ = State::Idle;
}

bool StateMachine::sendCommand(uint8_t flag) {
    if (!comms_) {
        return false;
    }
    // TODO: a false return means the ack was dropped (priority queue full)
    // and the GUI will sit waiting for it; decide on a retry/panic policy.
    return comms_->enqueuePriorityMessage(flag, nullptr, 0);
}

void StateMachine::update(uint32_t positionRaw, uint32_t forceRaw, uint32_t timeMs) {
    switch (state_) {
        case State::Idle:
            onIdle();
            break;
        case State::Armed:
            // Accepted a run request, waiting for GUI_run_start.
            // TODO: timeout back to Idle if the start never arrives.
            cycle_count = 0;
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

void StateMachine::onCalibration(uint32_t positionRaw, uint32_t forceRaw, uint32_t timeMs) {
    // TODO: seek -> slow approach -> stop-near-bottom sequence (design doc
    // open item 4, deferred until drive behavior is characterized). Likely
    // still wants to push Points to dataBuffer_ during this so the
    // calibration run is visible on the PC side -- confirm.
    (void)positionRaw;
    (void)forceRaw;
    (void)timeMs;
}

void StateMachine::onOperating(uint32_t positionRaw, uint32_t forceRaw, uint32_t timeMs) {
    // TODO: iterate the frequency list, >=5 cycles per frequency, while
    // continuously acquiring.
    //
    // Data streaming to the GUI is disabled for now (handshake-only MVP).
    // Re-enable by pushing a Point here:
    //   Point p;
    //   p.position = positionRaw;
    //   p.force = forceRaw;
    //   p.time_ms = timeMs;
    //   if (dataBuffer_) dataBuffer_->push(p);
    (void)positionRaw;
    (void)forceRaw;
    (void)timeMs;

    cycle_count += 1;

    double time_f = double(timeMs) / 1000000;
    Point p;
    p.position = uint32_t( 1000 + cos(time_f * 4) * 800 );
    p.force = forceRaw;
    p.time_ms = timeMs;
    if (dataBuffer_) dataBuffer_->push(p);


    if (cycle_count > 6000){
        sendCommand(Cmd::ESP_run_finished);
        enterIdle();
        cycle_count = 0;
    }
}

void StateMachine::onPanic() {
    // Nothing ongoing here -- entry into panic already stopped the motor
    // and attempted to notify the PC (see enterPanic()). Just wait for an
    // explicit "return to idle" instruction (handleInstruction) or a
    // hardware reset.
}

void StateMachine::enterPanic(FaultReason reason) {
    lastFault_ = reason;
    state_ = State::Panic;
    if (motor_) {
        motor_->stop();
    }
    // TODO: enqueue a best-effort error message onto the comms task's
    // priority queue, encoding 'reason' (see design doc section 2/5).
}

void StateMachine::enterIdle() {
    state_ = State::Idle;
    lastFault_ = FaultReason::None;
    if (motor_) {
        motor_->stop();
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
    //   - start test        -> state_ = State::Operating   (from Idle)
    //   - stop/cancel        -> enterIdle() (from any non-Panic state)
    //   - return to idle     -> enterIdle() (only valid from Panic, per
    //                            design doc section 2 -- panic does not
    //                            auto-recover)

    // Handshake-only MVP: replies carry no payload, and no drive checks are
    // done yet -- we just tell the GUI what it wants to hear. State is
    // changed before the reply is queued so the ESP is already in the new
    // state by the time the GUI reacts to the ack.
    switch (instr.flag) {
        case Cmd::GUI_check_connection:
            // Sent on initial connect. Reset to a known-safe state, then pong.
            enterIdle();
            sendCommand(Cmd::ESP_good_connection);
            break;
        case Cmd::GUI_run_request:
            if (state_ == State::Idle) {
                // TODO: check the drive is ready before accepting.
                //delay(1000);
                state_ = State::Armed;
                sendCommand(Cmd::ESP_run_accepted);
            }
            // TODO: reply with a rejection once one exists in the protocol.
            break;
        case Cmd::GUI_run_start:
            if (state_ == State::Armed) {
                //delay(1000);
                state_ = State::Operating;
                sendCommand(Cmd::ESP_run_starting);
            }
            break;
        case Cmd::GUI_abort:
            enterIdle();
            // TODO: ack with ESP_run_finished once the GUI has a Stopping state.
            break;
        default:
            break;
    }
}
