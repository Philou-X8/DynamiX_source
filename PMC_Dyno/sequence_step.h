#pragma once
#include <stdint.h>
#include <stddef.h>
#include <string.h>
#include "drive_protocol.h"

// One step of a run: run the drive at a frequency for a number of cycles.
// Sent by the GUI as GUI_send_sequence, payload (10 bytes):
//   [order][number of cycles][Modbus frame setting the drive frequency (8 bytes)]
// The frame is forwarded to the drive as-is; the target frequency is read back
// out of it so the run can tell when the drive got there.

constexpr size_t SEQUENCE_DRIVE_FRAME_SIZE = 8;
constexpr size_t SEQUENCE_STEP_PAYLOAD_SIZE = 2 + SEQUENCE_DRIVE_FRAME_SIZE;

struct SequenceStep {
    uint8_t cycles;
    uint16_t targetCentiHz; // 0.01 Hz, as written to the drive
    uint8_t driveFrame[SEQUENCE_DRIVE_FRAME_SIZE];
};

// Validates and decodes a GUI_send_sequence payload. Returns false if the size
// is wrong, the cycle count is 0, or the frame is not a good "write the set
// frequency" frame (valid CRC, function 0x06, register P00.10 at 0x000A or its
// RAM alias 0x800A).
inline bool parseSequenceStep(const uint8_t* payload, size_t size, uint8_t& order, SequenceStep& out) {
    if (size != SEQUENCE_STEP_PAYLOAD_SIZE || payload[1] == 0) {
        return false;
    }
    const uint8_t* frame = payload + 2;
    if (!modbusFrameCrcOk(frame, SEQUENCE_DRIVE_FRAME_SIZE)) {
        return false;
    }
    uint16_t reg = (uint16_t)(((uint16_t)frame[2] << 8) | frame[3]);
    if (frame[1] != 0x06 || (reg != 0x000A && reg != 0x800A)) {
        return false;
    }
    order = payload[0];
    out.cycles = payload[1];
    out.targetCentiHz = (uint16_t)(((uint16_t)frame[4] << 8) | frame[5]);
    memcpy(out.driveFrame, frame, SEQUENCE_DRIVE_FRAME_SIZE);
    return true;
}
