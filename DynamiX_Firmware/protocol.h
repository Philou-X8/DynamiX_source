#pragma once
#include <stdint.h>
#include <stddef.h>

// GUI link frame format. All multi-byte values are big-endian.
//
//   [0xFF][0x00][0xAA | 0x55][size][body ... size bytes]
//
// The 3rd byte is the frame type:
//   0xAA -> data body:    size / POINT_WIRE_SIZE points, back to back
//                          (each: 3B position, 3B force, 4B time, see Point)
//   0x55 -> command body: [flag][payload ...], so size = 1 + payload length.
//                          Flags are in command_flags.h.
//
// 'size' is one byte, so a body is at most 255 bytes: 25 points per data
// frame, 254 payload bytes per command.
//
// Must stay in sync with SerialDecoder.cs on the GUI side.

constexpr uint8_t SYNC_0 = 0xFF;
constexpr uint8_t SYNC_1 = 0x00;
constexpr uint8_t TYPE_DATA = 0xAA;
constexpr uint8_t TYPE_COMMAND = 0x55;
constexpr size_t HEADER_SIZE = 4;
constexpr size_t MAX_BODY_SIZE = 255;
constexpr size_t MAX_FRAME_SIZE = HEADER_SIZE + MAX_BODY_SIZE;
constexpr size_t MAX_COMMAND_PAYLOAD = MAX_BODY_SIZE - 1; // the flag takes one byte

// One measurement sample, as sent to the GUI. The wire layout (10 bytes) is
// written field by field by encodeDataFrame(), never by copying the struct.
struct Point {
    int32_t position; // raw ADC counts, signed 24-bit
    int32_t force;    // raw ADC counts, signed 24-bit
    uint32_t timeUs;  // microseconds since the run started
};

constexpr size_t POINT_WIRE_SIZE = 10;
constexpr size_t MAX_POINTS_PER_FRAME = MAX_BODY_SIZE / POINT_WIRE_SIZE; // 25

// A command received from the GUI: its flag and payload.
struct Command {
    uint8_t flag;
    uint8_t payload[MAX_COMMAND_PAYLOAD];
    size_t payloadSize;
};

// Builds a data frame holding 'count' points (1..MAX_POINTS_PER_FRAME).
// Returns the frame length, or 0 if it doesn't fit.
size_t encodeDataFrame(const Point* points, size_t count, uint8_t* out, size_t outSize);

// Builds a command frame: header, flag, payload ('payload' may be null when
// payloadSize is 0). Returns the frame length, or 0 if it doesn't fit.
size_t encodeCommandFrame(uint8_t flag, const uint8_t* payload, size_t payloadSize,
                          uint8_t* out, size_t outSize);

// Byte-at-a-time decoder for frames coming from the GUI. If a frame is cut
// short or garbled it drops back to looking for the next 0xFF 0x00 header.
class FrameDecoder {
public:
    // Feeds one byte. Returns true when 'out' holds a complete command.
    // Data frames (the GUI never sends any) are skipped.
    bool feed(uint8_t byte, Command& out);
    void reset();

private:
    enum class Step : uint8_t { Sync0, Sync1, Type, Size, Body };

    Step step_ = Step::Sync0;
    uint8_t type_ = 0;
    size_t size_ = 0;
    size_t received_ = 0;
    uint8_t body_[MAX_BODY_SIZE];
};
