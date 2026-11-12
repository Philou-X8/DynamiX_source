#pragma once
#include <stdint.h>
#include <stddef.h>
#include "motor_control.h"
#include "data_buffer.h"
#include "protocol.h"
#include "command_flags.h"

enum class State : uint8_t {
    Idle,
    Panic,
    Armed, // after getting a run request and making sure the drive is ready, wait here for the speed configurations
    Calibration,
    Operating,
};

// Reasons the state machine can enter Panic, matching design doc section 7.
enum class FaultReason : uint8_t {
    None,
    SensorStalled,      // sequence counter not incrementing
    SensorOutOfRange,    // saturated / implausible reading
    DriveFault,          // motor drive reported an error
    LoopTimingDrift       // main loop iteration overran its ~2ms budget
};

// A decoded command frame: the flag byte (see command_flags.h) plus its
// payload (everything after the flag, up to 254 bytes).
struct Instruction {
    uint8_t flag;
    uint8_t payload[MAX_PAYLOAD_SIZE - 1];
    size_t payloadSize;
};

// comms_task.h includes this header (for Instruction), so only forward
// declare here; state_machine.cpp includes the full definition.
class CommsTask;
class DriveComms;

class StateMachine {
public:
    // 'comms' is used to send replies/acks back to the GUI (via its priority
    // queue). May be null, in which case replies are silently skipped.
    // 'drive' is the Modbus link to the motor drive, used to stop it on abort.
    // May be null, in which case drive commands are skipped.
    void begin(MotorControl* motor, DoubleBuffer* dataBuffer, CommsTask* comms, DriveComms* drive);

    // Called once per main loop iteration (~500 Hz target).
    void update(uint32_t positionRaw, uint32_t forceRaw, uint32_t timeMs);

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
    void onCalibration(uint32_t positionRaw, uint32_t forceRaw, uint32_t timeMs);
    void onOperating(uint32_t positionRaw, uint32_t forceRaw, uint32_t timeMs);
    void onPanic();

    void enterPanic(FaultReason reason);
    void enterIdle();

    // Queue a payload-less command (flag only) for the GUI. Returns false
    // if comms isn't wired up or the priority queue is full.
    bool sendCommand(uint8_t flag);

    // Tell the motor drive to stop and set its speed to 0 (hardcoded Modbus
    // frames). Reports an error to the GUI if a frame couldn't be queued.
    void stopDrive();

    State state_ = State::Idle;
    FaultReason lastFault_ = FaultReason::None;

    MotorControl* motor_ = nullptr;
    DoubleBuffer* dataBuffer_ = nullptr;
    CommsTask* comms_ = nullptr;
    DriveComms* drive_ = nullptr;

    // TODO: operating-mode state (current target frequency, cycle count,
    // frequency list/index) once that part of the design is fleshed out.
    // TODO: calibration-mode state (seek/approach/stop sub-step).

    int cycle_count = 0;
};
