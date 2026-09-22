#include "motor_control.h"

void MotorControl::begin() {
    // TODO: init RS-485 UART / conversion card interface.
}

void MotorControl::setSpeed(float speedOrFreq) {
    // TODO: encode + send speed/frequency command over RS-485.
    (void)speedOrFreq;
}

void MotorControl::stop() {
    // TODO: send the drive's stop/zero-speed command. Called on entering
    // panic -- keep this path as simple/direct as possible, no dependency
    // on anything that could itself be in a bad state.
    setSpeed(0.0f);
}

bool MotorControl::hasFault() {
    // TODO: read drive status over RS-485.
    return false;
}
