#pragma once
#include <stdint.h>
#include <stddef.h>

class DriveLink;

// The TD350 motor drive, seen from the state machine: start, stop, set the
// frequency, read back the running frequency, and make sense of error
// answers. Builds the Modbus frames (drive_protocol.h) and hands them to the
// DriveLink, which sends them in the background.
//
// No Arduino dependency: the host tests run it against a fake DriveLink.
class MotorDrive {
public:
    void begin(DriveLink* link);

    bool runForward();                       // control command: forward running
    bool setFrequency(uint16_t centiHz);     // P00.10 in RAM, 0.01 Hz
    // Stops the motor: the stop command first (it replaces anything still
    // waiting to be sent), then the frequency back to 0 so a later start
    // doesn't resume at the old speed. Returns false if a frame was refused.
    bool stop();
    // A complete frame built elsewhere (a run step, the debug Hz button).
    bool sendFrame(const uint8_t* frame, size_t length);
    // Asks for the running frequency; the answer arrives later (see below).
    bool queryRunningFrequency();
    // Sets the drive's RS-485 timeout (P14.04, RAM only, 0.1 s; 0 = off).
    bool setCommTimeout(uint16_t deciseconds);

    // Call every main loop iteration, after everything else. Reads the
    // running frequency (the drive refuses the Modbus 08H echo) when nothing
    // was handed to the link for 'periodMs', so the drive's RS-485 timeout
    // only trips if the main loop (or the link) dies. The answer is a normal
    // running-frequency answer (runningFrequency()).
    void keepAlive(uint32_t nowMs, uint32_t periodMs);

    // Forgets the last running frequency, so runningFrequency() only reports
    // an answer that arrives from now on.
    void forgetRunningFrequency() { frequencyFresh_ = false; }
    // Latest running frequency answer (0.01 Hz) since forgetRunningFrequency().
    bool runningFrequency(uint16_t& centiHz) const {
        centiHz = runningCentiHz_;
        return frequencyFresh_;
    }

    // Reads the drive's answers. Running-frequency answers are kept for
    // runningFrequency(). Returns true at the first error answer, with a
    // description in 'text' (the error, and the last two frames sent, since
    // the answer doesn't say which request it refuses). Returns false once
    // there is nothing left to read.
    bool nextError(char* text, size_t textSize);

private:
    bool send(const uint8_t* frame, size_t length, bool urgent = false);
    bool write(uint16_t reg, uint16_t value, bool urgent = false);

    struct SentFrame {
        uint8_t function = 0;
        uint16_t reg = 0;
        uint16_t value = 0;
        bool valid = false;
    };

    DriveLink* link_ = nullptr;
    SentFrame recent_[2]; // newest first
    uint16_t runningCentiHz_ = 0;
    bool frequencyFresh_ = false;
    bool sentSinceKeepAlive_ = false; // a frame went to the link since the last keepAlive()
    uint32_t lastSendMs_ = 0;         // loop time of the last frame (or keep-alive attempt)
};
