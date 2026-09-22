#pragma once
#include <stdint.h>
#include <stddef.h>

// A single measurement sample.
//
// NOTE on layout: this struct is the in-memory / logical representation.
// It is deliberately NOT relied upon for the wire format (no #pragma pack,
// no assumptions about sizeof(Point) == 10) because natural alignment of
// the uint32_t member would pad this to 12 bytes on most compilers. The
// exact 10-byte wire representation (3B position + 3B force + 4B time) is
// produced explicitly by protocol.cpp's serialization code instead. See
// design doc section 4/5.
struct Point {
    int32_t position;   // 24-bit ADC reading, sign-extended
    int32_t force;       // 24-bit ADC reading, sign-extended
    uint32_t time_ms;    // ms since current test/run started
};

// Wire size of one point: 3B position + 3B force + 4B time.
constexpr size_t POINT_WIRE_SIZE = 10;

// 1-byte payload-size header field caps payload at 255 bytes.
// 255 / 10 = 25 points max per message.
constexpr size_t MAX_POINTS_PER_BUFFER = 25;

// Double buffer used to hand sensor samples from the main loop (producer)
// to the comms task (consumer) without the consumer blocking the producer.
//
// Usage:
//   - Main loop calls push() every sample. When push() returns false the
//     active write buffer is full; the main loop should call trySwap() (or
//     rely on the comms task doing so) before it can push again.
//   - Comms task calls trySwap() each time it's ready to send. If the
//     write buffer is full, it becomes the read buffer (returned via
//     outBuffer/outCount) and the other internal buffer becomes the new
//     write target.
//   - forceSwap() flushes whatever's currently in the write buffer even if
//     not full -- intended for state-machine exit / end of test run so
//     data doesn't sit unsent indefinitely (design doc, open item 2).
//
// Thread-safety: swap vs push is guarded internally by a FreeRTOS mutex.
// The critical section only covers pointer/index bookkeeping, not any
// data copying, so it stays fast even at 500 Hz.
class DoubleBuffer {
public:
    DoubleBuffer();

    // Producer side (main loop). Returns false if the active write buffer
    // is already full -- caller must not silently drop the sample; see
    // design doc open item 2 discussion for handling this case.
    bool push(const Point& p);

    // Consumer side (comms task). If the write buffer is full, swaps it to
    // become the read buffer, copies it into outBuffer (caller-provided,
    // must hold at least MAX_POINTS_PER_BUFFER points), sets outCount, and
    // returns true. Returns false (no-op) if the write buffer isn't full
    // yet.
    bool trySwap(Point* outBuffer, size_t& outCount);

    // Same as trySwap but swaps regardless of fill level (used on state
    // exit / end of run). Returns false only if there is nothing to send
    // (outCount would be 0).
    bool forceSwap(Point* outBuffer, size_t& outCount);

private:
    bool swapLocked(Point* outBuffer, size_t& outCount, bool force);

    Point buffers_[2][MAX_POINTS_PER_BUFFER];
    uint8_t writeIndex_;     // 0 or 1: which of buffers_[] is being written
    size_t writeCount_;      // how many points currently in the write buffer
    void* mutex_;            // SemaphoreHandle_t, opaque here to keep this
                              // header includable from non-Arduino contexts
};
