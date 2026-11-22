#pragma once
#include <stdint.h>
#include <stddef.h>
#include "protocol.h" // Point, MAX_POINTS_PER_FRAME

// Hands run points from the main loop (producer) to the GUI link's send task
// (consumer) without either waiting on the other.
//
// A ring of a few frames' worth of points: the send task takes them out one
// frame at a time and sends that copy while the main loop keeps adding. The
// spare frames absorb the send task being late (busy writing a message), which
// a single frame-sized buffer couldn't. The lock only covers bookkeeping and a
// 300-byte copy, so it stays short at 500 Hz.
class PointBuffer {
public:
    static constexpr size_t CAPACITY = 4 * MAX_POINTS_PER_FRAME; // 200 ms at 500 Hz

    void begin(); // creates the lock; call once before anything else

    // Main loop side. Returns false (and counts the point as dropped) when the
    // buffer is full because the send task fell behind.
    bool push(const Point& p);

    // Send task side. Moves the oldest points into 'out' (room for
    // MAX_POINTS_PER_FRAME points): a full frame's worth if there is one, or,
    // if 'partial', whatever is left. Returns the number of points moved
    // (0 = nothing taken).
    size_t take(Point* out, bool partial);

    // Forgets the points waiting to be sent and the dropped count. Called at
    // the start of a run so nothing of the previous one leaks into it.
    void reset();

    // Points refused by push() since the last reset().
    uint32_t dropped() const { return dropped_; }

private:
    Point points_[CAPACITY];
    size_t head_ = 0;  // oldest point
    size_t count_ = 0;
    volatile uint32_t dropped_ = 0;
    void* mutex_ = nullptr; // SemaphoreHandle_t, opaque so this header stays free of FreeRTOS
};
