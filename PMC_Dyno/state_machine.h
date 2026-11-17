#pragma once
#include <stdint.h>
#include <stddef.h>
#include "motor_control.h"
#include "data_buffer.h"
#include "protocol.h"
#include "command_flags.h"
#include "cycle_counter.h"
#include "sequence_step.h"

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
    LoopTimingDrift,      // main loop iteration overran its ~2ms budget
    CycleTimeout          // running at speed but no cycle completed for a long time
};

// A decoded command frame: the flag byte (see command_flags.h) plus its
// payload (everything after the flag, up to 254 bytes).
struct Instruction {
    uint8_t flag;
    uint8_t payload[MAX_PAYLOAD_SIZE - 1];
    size_t payloadSize;
};

// comms_task.h includes this header (for Instruction), so only forward
// declare here; state_machine.cpp includes the full definitions.
class CommsTask;
class DriveComms;
struct DriveFrame;

// Most steps a run can have. The GUI can build more (up to 255); the run
// request is refused above this.
constexpr size_t MAX_SEQUENCE_STEPS = 32;

class StateMachine {
public:
    // 'comms' is used to send replies/acks back to the GUI (via its priority
    // queue). May be null, in which case replies are silently skipped.
    // 'drive' is the Modbus link to the motor drive. May be null, in which
    // case drive commands are skipped (and a run can't start).
    void begin(MotorControl* motor, DoubleBuffer* dataBuffer, CommsTask* comms, DriveComms* drive);

    // Called once per main loop iteration (~500 Hz target).
    // 'timeMs' is the loop time in microseconds (micros()), despite the name.
    void update(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs);

    // Range of the position sensor seen so far. Call before update(): the
    // cycle counter thresholds (30% / 70%) are taken from it.
    void setPositionRange(int32_t min, int32_t max);

    // Called by the main loop when a pending instruction has been decoded
    // off the priority/instruction queue.
    void handleInstruction(const Instruction& instr);

    // Called by the main loop for each frame the motor drive sent back.
    void handleDriveResponse(const DriveFrame& frame);

    // Called by the main loop when it detects a fault condition itself
    // (e.g. a stalled sensor sequence counter, or its own loop timing
    // drift) -- see design doc section 7 for the full trigger list.
    void raiseFault(FaultReason reason);

    State current() const { return state_; }

private:
    // What a run is doing for the current step of the sequence.
    enum class RunPhase : uint8_t {
        SpeedingUp, // frequency sent to the drive, waiting for it to get there
        Counting    // at speed, counting cycles from the position sensor
    };

    void onIdle();
    void onCalibration(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs);
    void onOperating(int32_t positionRaw, int32_t forceRaw, uint32_t timeMs);
    void onPanic();

    void enterPanic(FaultReason reason);
    void enterIdle();

    // Queue a payload-less command (flag only) for the GUI. Returns false
    // if comms isn't wired up or the priority queue is full.
    bool sendCommand(uint8_t flag);

    // Queue an ESP_error with a text message for the GUI log.
    void sendError(const char* text);

    // Queue one Modbus frame for the drive. Returns false if it was refused.
    bool sendDriveFrame(const uint8_t* frame, size_t length);

    // Tell the motor drive to stop and set its speed to 0 (hardcoded Modbus
    // frames). Reports an error to the GUI if a frame couldn't be queued.
    void stopDrive();

    // --- Run sequence ---
    void startRun();                 // GUI_run_start: first step, clock reset
    bool beginStep();                // send the drive frequency of steps_[stepIndex_]
    void updateSpeedingUp(uint32_t runTimeUs);
    void updateCounting(int32_t positionRaw, uint32_t runTimeUs);
    void finishStep(uint32_t endUs); // separator, then next step or end of run

    State state_ = State::Idle;
    FaultReason lastFault_ = FaultReason::None;

    MotorControl* motor_ = nullptr;
    DoubleBuffer* dataBuffer_ = nullptr;
    CommsTask* comms_ = nullptr;
    DriveComms* drive_ = nullptr;

    // TODO: calibration-mode state (seek/approach/stop sub-step).

    // The run's steps, filled by GUI_send_sequence while Armed. A step is
    // stored at its 'order' so the sequence runs in the order the GUI meant,
    // and stepReceived_ lets GUI_run_start check that none were lost.
    SequenceStep steps_[MAX_SEQUENCE_STEPS];
    bool stepReceived_[MAX_SEQUENCE_STEPS];
    uint8_t stepsExpected_ = 0;  // from GUI_run_request
    uint8_t stepsReceived_ = 0;

    uint8_t stepIndex_ = 0;      // step being run
    RunPhase runPhase_ = RunPhase::SpeedingUp;
    bool runClockStarted_ = false;
    uint32_t runStartUs_ = 0;    // micros() at the first sample of the run; point times count from here
    uint32_t stepStartUs_ = 0;   // run time when the drive reached the step's speed
    uint32_t phaseStartMs_ = 0;  // millis() when the current phase began
    uint32_t lastQueryMs_ = 0;   // millis() of the last "read running frequency" request
    uint32_t lastCycleMs_ = 0;   // millis() of the last completed cycle (or start of counting)
    uint8_t cyclesDone_ = 0;
    CycleCounter cycleCounter_;

    // Latest running frequency the drive reported (0.01 Hz). Fresh = received
    // since the current step began.
    uint16_t driveFreqCentiHz_ = 0;
    bool driveFreqFresh_ = false;

    int32_t posMin_ = 0;
    int32_t posMax_ = 0;
};
