#pragma once
#include <stdint.h>

// One force/position pair from the ADC, both in raw signed 24-bit counts.
struct SensorSample {
    int32_t position;
    int32_t force;
    uint32_t timeUs;   // micros() when the force conversion was read (the position follows ~0.7 ms later)
    uint32_t sequence; // +1 for every new pair, so the reader can tell a new one from the last one
};
