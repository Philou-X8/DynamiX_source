#pragma once
#include <stdint.h>
#include <stddef.h>
#include "cycle_counter.h"
#include "printf_format.h"
#include "protocol.h"
#include "sensor_sample.h"
#include "sequence_step.h"
#include "signal_filters.h"

class GuiLink;
class MotorDrive;
class PointBuffer;

// The machine's states. Mirrors the GUI's MachineController:
//
//   Idle --GUI_run_request--> Armed --GUI_calibrate--> Calibration --done--> Armed
//        --GUI_send_sequence x N, GUI_run_start--> Operating --last step--> Idle
//   Idle --GUI_TAR_request / GUI_bottom_out--> Tar --done--> Idle
//   any motion state --fault--> Panic --GUI_abort--> Idle
//   any state --GUI_abort / GUI_check_connection--> Idle (motor stopped)
enum class State : uint8_t {
    Idle,        // motor stopped, nothing measured
    Panic,       // a fault stopped the motor; stays here until GUI_abort
    Armed,       // run accepted, waiting for the calibration, the sequence and the start
    Calibration, // run calibration: force at rest, then the stroke at a slow speed
    Operating,   // running the sequence and streaming points
    Tar,         // empty load (TAR only), then bottom out the scotch yoke
};

enum class FaultReason : uint8_t {
    SensorStalled,    // no new sensor sample while the motor is driven
    SensorOutOfRange, // a sensor input is saturated (overload, or a wire is off)
    DriveFault,       // the drive answered an error, or didn't reach / leave a speed
    CycleTimeout,     // the motor turns but the position doesn't cycle
    ForceFailsafe,    // the load cell reads over the limit
    StrokeNotFound,   // calibration / TAR couldn't find the stroke
};

// Most steps a run can have; a longer run request is refused.
constexpr size_t MAX_SEQUENCE_STEPS = 32;

// Owns every decision: what each GUI command does in each state, what the
// motor does, and when the GUI is told what. Runs on the main loop only.
//
// Time is passed in by the caller and nothing here touches the hardware
// directly, so the host tests drive it with simulated sensors and drive.
class StateMachine {
public:
    void begin(GuiLink* gui, MotorDrive* drive, PointBuffer* points);

    // Called by the main loop every tick (2 ms), in this order:
    void pollDrive();                                           // drive answers (errors, running frequency)
    void handleCommand(const Command& command, uint32_t nowMs); // each command received from the GUI
    void onSample(const SensorSample& sample, uint32_t nowMs);  // the new sensor pair, if there is one
    void tick(uint32_t nowMs);                                  // timeouts and stall detection

    State state() const { return state_; }

    // GUI_force_reboot was received and the motor stop is queued; the main
    // loop restarts the ESP once the stop had time to go out.
    bool rebootRequested() const { return rebootRequested_; }

private:
    enum class RunPhase : uint8_t {
        SpeedingUp, // step frequency sent, waiting for the drive to get there
        Counting,   // at speed, counting cycles
    };
    enum class CalibrationPhase : uint8_t {
        RestLoad,   // motor stopped: average the force with the shock mounted
        FindStroke, // motor turning slowly: record the position min/max
    };
    enum class TarPhase : uint8_t {
        Settle,        // motor stopped, let things settle
        EmptyLoad,     // average the force with nothing on the bench
        FindStroke,    // motor turning slowly: record the position min/max
        SeekBottom,    // wait for the yoke to pass the stop point on its way down
        Stopping,      // stop sent, wait for the drive to reach 0 Hz
        StoppedSettle, // stopped, let the yoke settle before reading where it is
    };
    enum class SpeedCheck : uint8_t { Waiting, Reached, TimedOut };

    // --- GUI commands
    void onRunRequest(const Command& command, uint32_t nowMs);
    void onCalibrate(const Command& command, uint32_t nowMs);
    void onSequenceStep(const Command& command);
    void onRunStart(uint32_t nowMs);
    void onTarRequest(bool measureEmptyLoad, uint32_t nowMs);
    void onDebugDriveFrame(const Command& command);
    void onReboot();

    // --- Per sample, per state
    bool safetyChecksPass();
    void updateCalibration(uint32_t nowMs);
    void updateOperating(const SensorSample& sample, uint32_t nowMs);
    void updateTar(uint32_t nowMs);

    // --- Run
    void startRun(uint32_t nowMs);
    bool beginStep(uint32_t nowMs);
    void finishStep(uint32_t endUs, uint32_t nowMs);

    // --- TAR
    bool startTarMotion(uint32_t nowMs);
    void finishTar();

    // --- Transitions
    void stopMotor();
    void goIdle();
    void abort();
    void enterMotionState(State state, uint32_t nowMs);
    void denyRun(const char* fmt, ...) PRINTF_FORMAT(2, 3);
    void enterPanic(FaultReason reason, const char* fmt = nullptr, ...) PRINTF_FORMAT(3, 4);

    // --- Helpers
    SpeedCheck checkSpeed(uint16_t targetCentiHz, uint32_t nowMs);
    void startPhaseClock(uint32_t nowMs);
    void startStrokeSearch(uint32_t nowMs);
    void sendCalibrationData(int32_t force);
    void report(uint8_t flag, const char* fmt, ...) PRINTF_FORMAT(3, 4);
    bool inMotionState() const;
    bool sensorAlive(uint32_t nowMs) const;
    int32_t bottomPermille(int32_t position) const;

    GuiLink* gui_ = nullptr;
    MotorDrive* drive_ = nullptr;
    PointBuffer* points_ = nullptr;

    State state_ = State::Idle;
    bool rebootRequested_ = false;

    // --- Sensors (every new sample)
    bool haveSample_ = false;
    uint32_t lastSampleMs_ = 0;
    Median5 positionFilter_;
    Median5 forceFilter_;
    int32_t position_ = 0;       // spike-filtered (median of 5)
    int32_t force_ = 0;          // spike-filtered (median of 5)
    SlowAverage forceAverage_;   // for "current force" reports
    InARow railInARow_;
    InARow overloadInARow_;

    // --- What the machine knows (kept between runs)
    bool strokeKnown_ = false;
    int32_t strokeMin_ = 0;
    int32_t strokeMax_ = 0;
    bool emptyLoadKnown_ = false;
    int32_t emptyLoad_ = 0;      // TAR: force with nothing on the bench
    int32_t restLoad_ = 0;       // calibration: force at rest with the shock mounted
    int32_t forceLimit_ = 0;     // failsafe, counts away from the empty load

    // --- Phase bookkeeping (Calibration, Operating, Tar)
    uint32_t phaseStartMs_ = 0;
    uint32_t lastQueryMs_ = 0;   // last running-frequency query
    RunningMean forceMean_;
    StrokeFinder strokeFinder_;

    // --- Armed: the run being prepared
    uint32_t armedSinceMs_ = 0;
    SequenceStep steps_[MAX_SEQUENCE_STEPS];
    bool stepReceived_[MAX_SEQUENCE_STEPS] = {};
    uint8_t stepsExpected_ = 0;
    uint8_t stepsReceived_ = 0;

    // --- Operating
    RunPhase runPhase_ = RunPhase::SpeedingUp;
    uint8_t stepIndex_ = 0;
    bool runClockStarted_ = false;
    uint32_t runStartUs_ = 0;    // sample time of the run's first point; point times count from here
    uint32_t stepStartUs_ = 0;   // run time when the step reached its speed
    uint32_t lastCycleMs_ = 0;
    uint8_t cyclesDone_ = 0;
    CycleCounter cycleCounter_;

    // --- Calibration / Tar
    CalibrationPhase calibrationPhase_ = CalibrationPhase::RestLoad;
    TarPhase tarPhase_ = TarPhase::Settle;
    bool tarMeasuresEmptyLoad_ = false;
    bool seekArmed_ = false;     // the yoke has been seen away from the bottom
};
