#pragma once
#include <stdint.h>
#include <stddef.h>
#include "data_buffer.h"

// Frame format (see design doc section 5):
//
//   [0xFF][0x00][0xAA | 0x55][size][payload ... size bytes]
//
// The 3rd sync byte doubles as the type flag:
//   0xAA -> data payload:        size / POINT_WIRE_SIZE Points, back to back
//   0x55 -> instruction payload: raw bytes, format defined by the
//                                 command/ack/error/heartbeat content itself
//
// 'size' is the payload length in bytes only (header not included), so
// max payload is 255 bytes -> max 25 Points per data frame.

constexpr uint8_t SYNC_0 = 0xFF;
constexpr uint8_t SYNC_1 = 0x00;
constexpr uint8_t TYPE_DATA = 0xAA;
constexpr uint8_t TYPE_INSTRUCTION = 0x55;
constexpr size_t HEADER_SIZE = 4;
constexpr size_t MAX_PAYLOAD_SIZE = 255;
constexpr size_t MAX_FRAME_SIZE = HEADER_SIZE + MAX_PAYLOAD_SIZE;

// Serializes 'count' points into a complete data frame (header + payload)
// in outBuf. Returns total bytes written, or 0 on error (count too large
// for outBufSize, or count > MAX_POINTS_PER_BUFFER).
size_t encodeDataFrame(const Point* points, size_t count,
                        uint8_t* outBuf, size_t outBufSize);

// Wraps an already-serialized instruction/ack/error/heartbeat payload into
// a complete instruction frame (header + payload) in outBuf. Returns total
// bytes written, or 0 on error (payloadSize > MAX_PAYLOAD_SIZE, or doesn't
// fit in outBufSize).
size_t encodeInstructionFrame(const uint8_t* payload, size_t payloadSize,
                               uint8_t* outBuf, size_t outBufSize);

enum class FrameType : uint8_t {
    Data,
    Instruction
};

struct DecodedFrame {
    FrameType type;
    uint8_t payload[MAX_PAYLOAD_SIZE];
    size_t payloadSize;

    // Convenience for FrameType::Data frames: reconstruct Points from
    // payload. Returns number of points decoded (payloadSize /
    // POINT_WIRE_SIZE), 0 if this isn't a data frame or payload size isn't
    // a whole multiple of POINT_WIRE_SIZE (malformed frame).
    size_t decodePoints(Point* outPoints, size_t maxPoints) const;
};

// Streaming, byte-at-a-time frame decoder for the inbound UART ISR/task.
// Resynchronizes automatically: if the sync sequence is ever violated
// mid-frame, it drops back to hunting for a fresh 0xFF 0x00 [0xAA|0x55].
class FrameDecoder {
public:
    FrameDecoder();

    // Feed one incoming byte. Returns true exactly when 'out' has been
    // filled with a complete, validated frame. Call repeatedly as bytes
    // arrive; internal state persists between calls.
    bool feed(uint8_t byte, DecodedFrame& out);

    void reset();

private:
    enum class State {
        WaitSync0,
        WaitSync1,
        WaitType,
        WaitSize,
        ReadPayload
    };

    State state_;
    uint8_t typeByte_;
    uint8_t expectedSize_;
    uint8_t buffer_[MAX_PAYLOAD_SIZE];
    size_t bytesRead_;
};
