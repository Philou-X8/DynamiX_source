#pragma once
#include <stdint.h>

// Thin wrapper around the RS-485 link to the motor drive via the
// conversion card. Open-loop from the drive's perspective: we send
// speed/frequency commands, it handles fine control (see design doc
// section 1/4).
class MotorControl {
public:
    void begin();

    // Command a running speed/frequency. Units/scaling TBD once the drive
    // is characterized.
    void setSpeed(float speedOrFreq);

    // Immediate stop. Called directly on entering panic (design doc
    // section 2/7) -- do not wait on anything else first.
    void stop();

    // Returns true if the drive reported a fault since the last check
    // (feeds the panic-trigger list in design doc section 7). Placeholder
    // until the drive's actual RS-485 status/error reporting is known.
    bool hasFault();

    // Calibration is expected to end up as a self-contained, sequential
    // "script style" routine (seek -> slow approach -> stop near bottom of
    // the sinusoid), not a generic reusable primitive. Left unimplemented
    // until the drive's real behavior is characterized -- see design doc
    // open item 4. Intended to be called repeatedly from the state
    // machine's onCalibration() until it reports done, or written as a
    // single blocking routine called once (TBD which fits better once the
    // drive is in hand).
    // void runCalibrationStep();

private:
    // TODO: RS-485/conversion-card interface details.
};
