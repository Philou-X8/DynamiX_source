#include "state_machine.h"

void StateMachine::begin(MotorControl* motor, DoubleBuffer* dataBuffer) {
    motor_ = motor;
    dataBuffer_ = dataBuffer;
    state_ = State::Idle;
}

void StateMachine::update(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs) {
    switch (state_) {
        case State::Idle:
            onIdle();
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
    // TODO: iterate the frequency list, >=5 cycles per frequency, while
    // continuously acquiring. For now, just demonstrate the data path:
    Point p;
    p.position = positionRaw;
    p.force = forceRaw;
    p.time_ms = timeMs;
    if (dataBuffer_) {
        dataBuffer_->push(p);
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
    // TODO: decode instr.raw per the finalized instruction payload format,
    // then dispatch:
    //   - start calibration -> state_ = State::Calibration (from Idle)
    //   - start test        -> state_ = State::Operating   (from Idle)
    //   - stop/cancel        -> enterIdle() (from any non-Panic state)
    //   - return to idle     -> enterIdle() (only valid from Panic, per
    //                            design doc section 2 -- panic does not
    //                            auto-recover)
    (void)instr;
}
