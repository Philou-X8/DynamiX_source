#include "motor_drive.h"
#include <stdio.h>
#include "bytes.h"
#include "drive_link.h"
#include "drive_protocol.h"

namespace {

// Error answer codes (manual 9.4.7)
const char* driveErrorName(uint8_t code) {
    switch (code) {
        case 0x01: return "invalid command";
        case 0x02: return "invalid address";
        case 0x03: return "invalid data";
        case 0x04: return "operation failed";
        case 0x05: return "password error";
        case 0x06: return "frame error";
        case 0x07: return "read-only";
        case 0x08: return "can't change while running";
        case 0x09: return "password protected";
        default:   return "unknown";
    }
}

} // namespace

void MotorDrive::begin(DriveLink* link) {
    link_ = link;
}

bool MotorDrive::send(const uint8_t* frame, size_t length, bool urgent) {
    if (link_ == nullptr || !link_->sendFrame(frame, length, urgent)) {
        return false;
    }
    if (length >= 6) {
        recent_[1] = recent_[0];
        recent_[0].function = frame[1];
        recent_[0].reg = readUint16BE(frame + 2);
        recent_[0].value = readUint16BE(frame + 4);
        recent_[0].valid = true;
    }
    sentSinceKeepAlive_ = true;
    return true;
}

bool MotorDrive::write(uint16_t reg, uint16_t value, bool urgent) {
    uint8_t frame[MODBUS_REQUEST_SIZE];
    buildWriteRequest(DRIVE_ADDRESS, reg, value, frame);
    return send(frame, sizeof(frame), urgent);
}

bool MotorDrive::runForward() {
    return write(DRIVE_REG_CONTROL, DRIVE_CONTROL_FORWARD);
}

bool MotorDrive::setFrequency(uint16_t centiHz) {
    return write(DRIVE_REG_SET_FREQUENCY_RAM, centiHz);
}

bool MotorDrive::stop() {
    const bool stopped = write(DRIVE_REG_CONTROL, DRIVE_CONTROL_STOP, /*urgent=*/true);
    const bool zeroed = setFrequency(0);
    return stopped && zeroed;
}

bool MotorDrive::sendFrame(const uint8_t* frame, size_t length) {
    return send(frame, length);
}

bool MotorDrive::queryRunningFrequency() {
    uint8_t frame[MODBUS_REQUEST_SIZE];
    buildReadRequest(DRIVE_ADDRESS, DRIVE_REG_RUNNING_FREQUENCY, 1, frame);
    return send(frame, sizeof(frame));
}

bool MotorDrive::resetFault() {
    return write(DRIVE_REG_CONTROL, DRIVE_CONTROL_FAULT_RESET);
}

bool MotorDrive::setCommTimeout(uint16_t deciseconds) {
    return write(DRIVE_REG_COMM_TIMEOUT_RAM, deciseconds);
}

void MotorDrive::keepAlive(uint32_t nowMs, uint32_t periodMs) {
    if (sentSinceKeepAlive_) { // something went out during this loop iteration
        sentSinceKeepAlive_ = false;
        lastSendMs_ = nowMs;
        return;
    }
    if (nowMs - lastSendMs_ < periodMs) {
        return;
    }
    constexpr uint16_t ECHO_DATA = 0x5AA5; // any value, the drive sends it back
    uint8_t frame[MODBUS_REQUEST_SIZE];
    buildEchoRequest(DRIVE_ADDRESS, ECHO_DATA, frame);
    send(frame, sizeof(frame)); // refused = queue full: frames are going out anyway
    sentSinceKeepAlive_ = false;
    lastSendMs_ = nowMs;        // either way, try again one period from now
}

bool MotorDrive::nextError(char* text, size_t textSize) {
    if (link_ == nullptr) {
        return false;
    }
    DriveFrame reply;
    while (link_->popResponse(reply)) {
        uint16_t value;
        if (parseReadResponseOneRegister(reply.bytes, reply.length, DRIVE_ADDRESS, value)) {
            runningCentiHz_ = value; // the only register this code reads
            frequencyFresh_ = true;
            continue;
        }
        uint8_t function;
        uint8_t code;
        if (!parseExceptionResponse(reply.bytes, reply.length, DRIVE_ADDRESS, function, code)) {
            continue; // a write echoed back, or noise
        }
        // e.g. "Drive error 0x03 (invalid data) on function 0x06 | last sent: 0x06 reg 0x2000 = 0x0001 ; 0x06 reg 0x800A = 0x03E8"
        int n = snprintf(text, textSize, "Drive error 0x%02X (%s) on function 0x%02X | last sent:",
                         code, driveErrorName(code), function);
        for (int i = 0; i < 2 && n > 0 && (size_t)n < textSize; i++) {
            if (recent_[i].valid) {
                n += snprintf(text + n, textSize - n, "%s 0x%02X reg 0x%04X = 0x%04X", i == 0 ? "" : " ;",
                              recent_[i].function, recent_[i].reg, recent_[i].value);
            }
        }
        return true;
    }
    return false;
}
