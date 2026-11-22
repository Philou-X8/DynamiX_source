#pragma once
#include <stdint.h>
#include <stddef.h>
#include "cycle_counter.h"
#include "printf_format.h"
#include "protocol.h"
#include "sensor_sample.h"
#include "sequence_step.h"
#include "signal_filters.h"
#include "stroke_tools.h"

class GuiLink;
class MotorDrive;
class PointBuffer;

// The machine's states. Mirrors the GUI's MachineController:
//
//   Idle --GUI_run_request--> Armed --GUI_calibrate--> Calibration --done--> Armed
//        --GUI_send_sequence x N, GUI_run_start--> Operating --last step, bottom out--> Idle
//   Idle --GUI_TAR_request / GUI_bottom_out--> Tar --done--> Idle
//   any motion state --fault--> Panic --GUI_abort--> Idle
//   any state --safety circuit open (pin DRIVE_READY not at DRIVE_READY_LEVEL)--> Panic
//        --reset button on the machine (relay closed again)--> Idle
//   any state --GUI_abort / GUI_check_connection--> Idle (motor stopped),
//        except Panic while the safety circuit is open
//   any state --no sign of life from the GUI for GUI_TIMEOUT_MS--> Idle (motor stopped),
//        except Panic, which stays. The GUI is told when it comes back (ESP_not_connected).
enum class State : uint8_t {
    Idle,        // motor stopped, nothing measured
    Panic,       // a fault stopped the motor. Left by the machine's reset button for a
                 // safety circuit fault, by GUI_abort for the others (circuit closed)
    Armed,       // run accepted, waiting for the calibration, the sequence and the start
    Calibration, // run calibration: bottom force, stroke, top force
    Operating,   // running the sequence and streaming points, then the bottom out
    Tar,         // empty load (TAR only), stroke, then bottom out the scotch yoke
};

enum class FaultReason : uint8_t {
    SensorStalled,    // no new sensor sample while the motor is driven
    SensorOutOfRange, // a sensor input is saturated (overload, or a wire is off)
    DriveFault,       // the drive answered an error, or didn't reach / leave a speed
    CycleTimeout,     // the motor turns but the position doesn't cycle
    ForceFailsafe,    // the load cell reads over the limit
    StrokeNotFound,   // calibration / TAR couldn't find the stroke
    SafetyCircuit,    // the drive's ready relay opened: e-stop, door, or a drive fault
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
    void tick(uint32_t nowMs);                                  // timeouts, stall detection, live data

    // Called every main loop iteration with the drive's ready relay (pin
    // DRIVE_READY): true = pin at DRIVE_READY_LEVEL = safety circuit closed, drive ready.
    void onSafetyInput(bool ready, uint32_t nowMs);

    // Newest temperature (0.01 degC, 0 = unknown), for the live data. Called
    // every main loop iteration.
    void setTemperature(int32_t centiCelsius) { temperature_ = centiCelsius; }

    // Turns on the drive's RS-485 timeout. Called at boot (after the stop)
    // and when the GUI connects. Never resets a drive fault.
    void prepareDrive();

    State state() const { return state_; }

    // GUI_force_reboot was received and the motor stop is queued; the main
    // loop restarts the ESP once the stop had time to go out.
    bool rebootRequested() const { return rebootRequested_; }

private:
    enum class RunPhase : uint8_t {
        SpeedingUp, // step frequency sent, waiting for the drive to get there
        Counting,   // at speed, counting cycles
        BottomOut,  // every step done, no more points: parking the yoke at the bottom
    };
    enum class CalibrationPhase : uint8_t {
        BottomForce, // motor stopped (yoke near the bottom): average the force
        FindStroke,  // a little over one cycle at CALIBRATION_CENTIHZ: where the yoke turns back
        ApproachTop, // on the way up: slow down to CALIBRATION_TOP_CENTIHZ before the top
        TopForce,    // average the force through the top
    };
    enum class TarPhase : uint8_t {
        EmptyLoad,  // motor stopped: average the force with nothing on the bench (TAR only)
        FindStroke, // motor turning slowly: record the position min/max
        BottomOut,  // park the yoke at the bottom
    };
    // The bottom out, shared by the TAR, GUI_bottom_out and the end of a run.
    enum class BottomOutPhase : uint8_t {
        ReachSpeed, // TAR_CENTIHZ sent, waiting for the drive to get there
        Seek,       // wait for the yoke to pass the stop point on its way down
        Stopping,   // stop sent, wait for the drive to reach 0 Hz
        Settle,     // stopped, let the yoke settle before reading where it is
    };
    enum class SpeedCheck : uint8_t { Waiting, Reached, TimedOut };
    enum class Progress : uint8_t { Busy, Done, Failed }; // Failed: already in Panic

    // --- GUI commands
    void onRunRequest(const Command& command, uint32_t nowMs);
    void onCalibrate(uint32_t nowMs);
    void onSetFailsafe(const Command& command);
    void onSequenceStep(const Command& command);
    void onRunStart(uint32_t nowMs);
    void onTarRequest(bool measureEmptyLoad, uint32_t nowMs);
    void onDebugDriveFrame(const Command& command);
    void onReboot();
    void onGuiLost(uint32_t nowMs);

    // --- Per sample, per state
    bool safetyChecksPass();
    void updateCalibration(uint32_t nowMs);
    void updateOperating(const SensorSample& sample, uint32_t nowMs);
    void updateTar(uint32_t nowMs);

    // --- Run
    void startRun(uint32_t nowMs);
    bool beginStep(uint32_t nowMs);
    void finishStep(uint32_t endUs, uint32_t nowMs);
    void finishRun();

    // --- Calibration
    void finishCalibration(uint32_t nowMs);

    // --- TAR
    bool startTarMotion(uint32_t nowMs);
    void finishTar();

    // --- Bottom out (any motion state)
    bool startBottomOut(uint32_t nowMs);
    Progress updateBottomOut(uint32_t nowMs);
    double bottomPercent() const;

    // --- Transitions
    void stopMotor();
    void goIdle();
    void abort();
    void leavePanic();
    bool refuseIfSafetyOpen(const char* what);
    void enterMotionState(State state, uint32_t nowMs);
    void denyRun(const char* fmt, ...) PRINTF_FORMAT(2, 3);
    void enterPanic(FaultReason reason, const char* fmt = nullptr, ...) PRINTF_FORMAT(3, 4);

    // --- Helpers
    SpeedCheck checkSpeed(uint16_t targetCentiHz, uint32_t nowMs);
    void startPhaseClock(uint32_t nowMs);
    void startStrokeSearch(uint32_t nowMs);
    void setStroke(int32_t strokeMin, int32_t strokeMax);
    bool startMotor(uint16_t centiHz);
    void sendCalibrationData();
    void sendTarData(int32_t force);
    void sendLiveData(uint32_t nowMs);
    void report(uint8_t flag, const char* fmt, ...) PRINTF_FORMAT(3, 4);
    bool inMotionState() const;
    bool sensorAlive(uint32_t nowMs) const;

    GuiLink* gui_ = nullptr;
    MotorDrive* drive_ = nullptr;
    PointBuffer* points_ = nullptr;

    State state_ = State::Idle;
    bool rebootRequested_ = false;

    // --- Safety circuit
    enum class Safety : uint8_t { Unknown, Open, Closed };
    Safety safety_ = Safety::Unknown;  // debounced
    bool safetyRaw_ = false;
    uint32_t safetyRawSinceMs_ = 0;
    FaultReason panicReason_ = FaultReason::SafetyCircuit;
    bool panicNeedsGui_ = false;       // a software fault: GUI_abort acknowledges it
    uint32_t lastLiveDataMs_ = 0;
    int32_t temperature_ = 0;          // 0.01 degC, 0 = unknown

    // --- GUI link: linked by the first command, dropped after GUI_TIMEOUT_MS without one
    bool guiLinked_ = false;
    uint32_t guiLastMs_ = 0;           // when the last command arrived

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
    int32_t bottomForce_ = 0;    // calibration: at rest near the bottom, shock mounted
    int32_t topForce_ = 0;       // calibration: through the top at slow speed
    int32_t forceMin_ = 0;       // failsafe window, raw counts (GUI_set_failsafe)
    int32_t forceMax_ = 0;

    // --- Phase bookkeeping (Calibration, Operating, Tar)
    uint32_t phaseStartMs_ = 0;
    uint32_t lastQueryMs_ = 0;   // last running-frequency query
    RestForce restForce_;        // TAR empty load, calibration bottom force
    StrokeFinder strokeFinder_;  // TAR: stroke from a settled range
    BottomOutPhase bottomOutPhase_ = BottomOutPhase::ReachSpeed;
    EndApproach approach_;       // bottom out stop point, calibration slow-down point

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

    // --- Calibration
    CalibrationPhase calibrationPhase_ = CalibrationPhase::BottomForce;
    uint32_t calibrationMotionStartMs_ = 0;
    TurnStrokeFinder turnFinder_;
    EndForceAverage topForceAverage_;

    // --- Tar
    TarPhase tarPhase_ = TarPhase::EmptyLoad;
    bool tarMeasuresEmptyLoad_ = false;
};
