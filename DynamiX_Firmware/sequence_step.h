#pragma once
#include <stdint.h>
#include <stddef.h>
#include <string.h>
#include "drive_protocol.h"

// One step of a run: run the drive at a frequency for a number of cycles.
// Sent by the GUI as GUI_send_sequence, payload (10 bytes):
//   [order][number of cycles][Modbus frame setting the drive frequency (8 bytes)]
// The frame is forwarded to the drive as-is; the target frequency is read
// back out of it so the run can tell when the drive got there.

constexpr size_t SEQUENCE_STEP_PAYLOAD_SIZE = 2 + MODBUS_REQUEST_SIZE;

struct SequenceStep {
    uint8_t cycles;
    uint16_t targetCentiHz; // 0.01 Hz, as written to the drive
    uint8_t driveFrame[MODBUS_REQUEST_SIZE];
};

// Validates and decodes a GUI_send_sequence payload. Returns false if the
// size is wrong, the cycle count is 0, or the frame isn't a valid write of
// the set frequency to the drive.
inline bool parseSequenceStep(const uint8_t* payload, size_t size, uint8_t& order, SequenceStep& out) {
    if (size != SEQUENCE_STEP_PAYLOAD_SIZE || payload[1] == 0) {
        return false;
    }
    const uint8_t* frame = payload + 2;
    uint16_t centiHz;
    if (!parseSetFrequencyRequest(frame, MODBUS_REQUEST_SIZE, centiHz)) {
        return false;
    }
    order = payload[0];
    out.cycles = payload[1];
    out.targetCentiHz = centiHz;
    memcpy(out.driveFrame, frame, MODBUS_REQUEST_SIZE);
    return true;
}
