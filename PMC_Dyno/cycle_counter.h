#pragma once
#include <stdint.h>

// Counts strokes of the machine from the position sensor, with a flip-flop
// that has hysteresis: the position has to rise above 70% of its range
// ("flip") and then drop back below 30% ("flop") for one cycle to count.
// The 30%/70% gap keeps noise around a single threshold from counting twice.
//
// Header-only and free of Arduino dependencies so it can be checked on a PC.
class CycleCounter {
public:
    // Ranges narrower than this (in sensor counts) are treated as "no real
    // stroke seen yet" and nothing is counted. TUNE: it must sit above the
    // sensor noise but well below the real stroke.
    static constexpr int32_t MIN_RANGE = 10000;

    // Forget the current phase; call when a new step starts counting.
    void reset() {
        state_ = Unknown;
        armed_ = false;
    }

    // Feed one position sample with the current range of the position.
    // Returns true when this sample completes a cycle.
    //
    // A cycle is Low -> High -> Low. Counting only starts after the first
    // time the position was seen Low, so a step that starts mid-stroke
    // doesn't count a partial first cycle.
    bool update(int32_t position, int32_t rangeMin, int32_t rangeMax) {
        int64_t span = (int64_t)rangeMax - (int64_t)rangeMin;
        if (span < MIN_RANGE) {
            return false;
        }
        int64_t low = rangeMin + span * 30 / 100;
        int64_t high = rangeMin + span * 70 / 100;

        switch (state_) {
            case Unknown:
                if (position >= high) {
                    state_ = High;
                } else if (position <= low) {
                    state_ = Low;
                    armed_ = true;
                }
                return false;
            case Low:
                if (position >= high) {
                    state_ = High;
                }
                return false;
            case High:
                if (position <= low) {
                    state_ = Low;
                    bool completed = armed_;
                    armed_ = true;
                    return completed;
                }
                return false;
        }
        return false;
    }

private:
    enum Phase : uint8_t { Unknown, Low, High };
    Phase state_ = Unknown;
    bool armed_ = false; // the position has been Low since reset()
};
