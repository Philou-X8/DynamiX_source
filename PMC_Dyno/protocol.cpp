#include "protocol.h"
#include <string.h>

// --- Point serialization -----------------------------------------------
// Wire layout per point (10 bytes), big-endian (most significant byte
// first), all fields unsigned:
//   [0..2]  position, 24 bits
//   [3..5]  force,    24 bits
//   [6..9]  time_ms,  32 bits
//
// Must stay in sync with SerialDecoder.DecodeMessage() on the PC side.

void writeUint24BE(uint32_t v, uint8_t* out) {
    out[0] = (uint8_t)((v >> 16) & 0xFF);
    out[1] = (uint8_t)((v >> 8) & 0xFF);
    out[2] = (uint8_t)(v & 0xFF);
}

static uint32_t readUint24(const uint8_t* b) {
    return ((uint32_t)b[0] << 16) | ((uint32_t)b[1] << 8) | (uint32_t)b[2];
}

static void writePointBytes(const Point& p, uint8_t* out) {
    // writeUint24 keeps only the low 24 bits, so an out-of-range value
    // can't leak into the neighbouring field.
    writeUint24BE(p.position, out);
    writeUint24BE(p.force, out + 3);

    out[6] = (uint8_t)((p.time_ms >> 24) & 0xFF);
    out[7] = (uint8_t)((p.time_ms >> 16) & 0xFF);
    out[8] = (uint8_t)((p.time_ms >> 8) & 0xFF);
    out[9] = (uint8_t)(p.time_ms & 0xFF);
}

static void readPointBytes(const uint8_t* in, Point& p) {
    p.position = readUint24(in);
    p.force = readUint24(in + 3);
    p.time_ms = ((uint32_t)in[6] << 24) | ((uint32_t)in[7] << 16) |
                ((uint32_t)in[8] << 8) | (uint32_t)in[9];
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

size_t encodeCommandFrame(uint8_t flag, const uint8_t* payload, size_t payloadSize,
                           uint8_t* outBuf, size_t outBufSize) {
    // Body = [flag][payload...], so size counts the flag byte too.
    const size_t bodySize = 1 + payloadSize;
    if (bodySize > MAX_PAYLOAD_SIZE) {
        return 0;
    }
    const size_t totalSize = HEADER_SIZE + bodySize;
    if (totalSize > outBufSize) {
        return 0;
    }

    outBuf[0] = SYNC_0;
    outBuf[1] = SYNC_1;
    outBuf[2] = TYPE_INSTRUCTION;
    outBuf[3] = (uint8_t)bodySize;
    outBuf[4] = flag;

    if (payloadSize > 0) {
        memcpy(outBuf + HEADER_SIZE + 1, payload, payloadSize);
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
                if (typeByte_ == TYPE_INSTRUCTION) {
                    // A command always carries at least its flag byte, so
                    // size 0 is malformed -- drop it and resync.
                    reset();
                    return false;
                }
                // Zero-length data frame: complete frame immediately.
                out.type = FrameType::Data;
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
