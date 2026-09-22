#pragma once
#include <stdint.h>
#include <stddef.h>
#include "motor_control.h"
#include "data_buffer.h"

enum class State : uint8_t {
    Idle,
    Calibration,
    Operating,
    Panic
};

// Reasons the state machine can enter Panic, matching design doc section 7.
enum class FaultReason : uint8_t {
    None,
    SensorStalled,      // sequence counter not incrementing
    SensorOutOfRange,    // saturated / implausible reading
    DriveFault,          // motor drive reported an error
    LoopTimingDrift       // main loop iteration overran its ~2ms budget
};

// TODO: replace with the real decoded-instruction type once protocol
// payload formats for instructions are defined.
struct Instruction {
    uint8_t raw[64];
    size_t size;
};

class StateMachine {
public:
    void begin(MotorControl* motor, DoubleBuffer* dataBuffer);

    // Called once per main loop iteration (~500 Hz target).
    void update(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs);

    // Called by the main loop when a pending instruction has been decoded
    // off the priority/instruction queue.
    void handleInstruction(const Instruction& instr);

    // Called by the main loop when it detects a fault condition itself
    // (e.g. a stalled sensor sequence counter, or its own loop timing
    // drift) -- see design doc section 7 for the full trigger list.
    void raiseFault(FaultReason reason);

    State current() const { return state_; }

private:
    void onIdle();
    void onCalibration(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs);
    void onOperating(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs);
    void onPanic();

    void enterPanic(FaultReason reason);
    void enterIdle();

    State state_ = State::Idle;
    FaultReason lastFault_ = FaultReason::None;

    MotorControl* motor_ = nullptr;
    DoubleBuffer* dataBuffer_ = nullptr;

    // TODO: operating-mode state (current target frequency, cycle count,
    // frequency list/index) once that part of the design is fleshed out.
    // TODO: calibration-mode state (seek/approach/stop sub-step).
};
