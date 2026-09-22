#include "protocol.h"
#include <string.h>

// --- Point serialization -----------------------------------------------
// Wire layout per point (10 bytes), little-endian:
//   [0..2]  position, low 24 bits of the int32_t, two's complement
//   [3..5]  force,    low 24 bits of the int32_t, two's complement
//   [6..9]  time_ms, full 32 bits
//
// NOTE: pick an endianness and keep the PC-side decoder consistent with
// it. Little-endian chosen here since it matches the ESP32's native byte
// order (no swapping needed on the firmware side).

static void writePointBytes(const Point& p, uint8_t* out) {
    out[0] = (uint8_t)(p.position & 0xFF);
    out[1] = (uint8_t)((p.position >> 8) & 0xFF);
    out[2] = (uint8_t)((p.position >> 16) & 0xFF);

    out[3] = (uint8_t)(p.force & 0xFF);
    out[4] = (uint8_t)((p.force >> 8) & 0xFF);
    out[5] = (uint8_t)((p.force >> 16) & 0xFF);

    out[6] = (uint8_t)(p.time_ms & 0xFF);
    out[7] = (uint8_t)((p.time_ms >> 8) & 0xFF);
    out[8] = (uint8_t)((p.time_ms >> 16) & 0xFF);
    out[9] = (uint8_t)((p.time_ms >> 24) & 0xFF);
}

// Sign-extend a 24-bit two's complement value read from the wire into a
// 32-bit int.
static int32_t readInt24(const uint8_t* b) {
    int32_t v = (int32_t)b[0] | ((int32_t)b[1] << 8) | ((int32_t)b[2] << 16);
    if (v & 0x00800000) {
        v |= 0xFF000000; // sign-extend
    }
    return v;
}

static void readPointBytes(const uint8_t* in, Point& p) {
    p.position = readInt24(in);
    p.force = readInt24(in + 3);
    p.time_ms = (uint32_t)in[6] | ((uint32_t)in[7] << 8) |
                ((uint32_t)in[8] << 16) | ((uint32_t)in[9] << 24);
}

// --- Encoding ------------------------------------------------------------

size_t encodeDataFrame(const Point* points, size_t count,
                        uint8_t* outBuf, size_t outBufSize) {
    if (count == 0 || count > MAX_POINTS_PER_BUFFER) {
        return 0;
    }
    const size_t payloadSize = count * POINT_WIRE_SIZE;
    const size_t totalSize = HEADER_SIZE + payloadSize;
    if (totalSize > outBufSize || payloadSize > MAX_PAYLOAD_SIZE) {
        return 0;
    }

    outBuf[0] = SYNC_0;
    outBuf[1] = SYNC_1;
    outBuf[2] = TYPE_DATA;
    outBuf[3] = (uint8_t)payloadSize;

    uint8_t* payloadPtr = outBuf + HEADER_SIZE;
    for (size_t i = 0; i < count; i++) {
        writePointBytes(points[i], payloadPtr + i * POINT_WIRE_SIZE);
    }
    return totalSize;
}

size_t encodeInstructionFrame(const uint8_t* payload, size_t payloadSize,
                               uint8_t* outBuf, size_t outBufSize) {
    if (payloadSize > MAX_PAYLOAD_SIZE) {
        return 0;
    }
    const size_t totalSize = HEADER_SIZE + payloadSize;
    if (totalSize > outBufSize) {
        return 0;
    }

    outBuf[0] = SYNC_0;
    outBuf[1] = SYNC_1;
    outBuf[2] = TYPE_INSTRUCTION;
    outBuf[3] = (uint8_t)payloadSize;

    if (payloadSize > 0) {
        memcpy(outBuf + HEADER_SIZE, payload, payloadSize);
    }
    return totalSize;
}

// --- DecodedFrame::decodePoints ------------------------------------------

size_t DecodedFrame::decodePoints(Point* outPoints, size_t maxPoints) const {
    if (type != FrameType::Data || payloadSize % POINT_WIRE_SIZE != 0) {
        return 0;
    }
    size_t count = payloadSize / POINT_WIRE_SIZE;
    if (count > maxPoints) {
        count = maxPoints;
    }
    for (size_t i = 0; i < count; i++) {
        readPointBytes(payload + i * POINT_WIRE_SIZE, outPoints[i]);
    }
    return count;
}

// --- FrameDecoder ---------------------------------------------------------

FrameDecoder::FrameDecoder() {
    reset();
}

void FrameDecoder::reset() {
    state_ = State::WaitSync0;
    typeByte_ = 0;
    expectedSize_ = 0;
    bytesRead_ = 0;
}

bool FrameDecoder::feed(uint8_t byte, DecodedFrame& out) {
    switch (state_) {
        case State::WaitSync0:
            if (byte == SYNC_0) {
                state_ = State::WaitSync1;
            }
            // else: stay hunting for sync
            return false;

        case State::WaitSync1:
            if (byte == SYNC_1) {
                state_ = State::WaitType;
            } else if (byte == SYNC_0) {
                // stay in WaitSync1 (allows 0xFF 0xFF 0x00 ... to resync)
            } else {
                state_ = State::WaitSync0;
            }
            return false;

        case State::WaitType:
            if (byte == TYPE_DATA || byte == TYPE_INSTRUCTION) {
                typeByte_ = byte;
                state_ = State::WaitSize;
            } else {
                // Not a valid type flag -- resync.
                state_ = State::WaitSync0;
            }
            return false;

        case State::WaitSize:
            expectedSize_ = byte;
            bytesRead_ = 0;
            if (expectedSize_ == 0) {
                // Zero-length payload is a complete frame immediately.
                out.type = (typeByte_ == TYPE_DATA) ? FrameType::Data
                                                      : FrameType::Instruction;
                out.payloadSize = 0;
                reset();
                return true;
            }
            state_ = State::ReadPayload;
            return false;

        case State::ReadPayload:
            buffer_[bytesRead_++] = byte;
            if (bytesRead_ >= expectedSize_) {
                out.type = (typeByte_ == TYPE_DATA) ? FrameType::Data
                                                      : FrameType::Instruction;
                out.payloadSize = bytesRead_;
                memcpy(out.payload, buffer_, bytesRead_);
                reset();
                return true;
            }
            return false;
    }
    return false; // unreachable
}
