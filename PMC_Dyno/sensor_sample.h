#pragma once
#include <stdint.h>
#include "bytes.h"
#include "config.h"

// One force/position pair from the ADC, both in raw signed 24-bit counts.
struct SensorSample {
    int32_t position;
    int32_t force;
    uint32_t timeUs;   // micros() when the force conversion was read (the position follows ~0.7 ms later)
    uint32_t sequence; // +1 for every new pair, so the reader can tell a new one from the last one
};

// The raw force with the sign set by Config::INVERT_FORCE. -INT24_MIN doesn't
// fit in 24 bits: it becomes INT24_MAX, so a saturated input stays at a rail.
inline int32_t orientForce(int32_t raw) {
    return Config::INVERT_FORCE ? clampInt24(-raw) : raw;
}
