#pragma once
#include <stdint.h>

// Counts strokes of the machine from the position, with a flip-flop that has
// hysteresis: the position has to rise above the high threshold ("flip") and
// then drop back below the low one ("flop") for one cycle to count. The gap
// between the two keeps noise around a single threshold from counting twice.
//
// Header-only and free of Arduino dependencies so the host tests check it.
class CycleCounter {
public:
    // Sets the thresholds from the stroke (lowest/highest position) and
    // forgets the current phase. Call when a step starts counting.
    void start(int32_t strokeMin, int32_t strokeMax, int32_t lowPercent, int32_t highPercent) {
        const int64_t span = (int64_t)strokeMax - (int64_t)strokeMin;
        low_ = strokeMin + span * lowPercent / 100;
        high_ = strokeMin + span * highPercent / 100;
        state_ = Unknown;
        armed_ = false;
    }

    // Feed one (spike-filtered) position. Returns true when this sample
    // completes a cycle.
    //
    // A cycle is Low -> High -> Low. Counting only starts once the position
    // has been seen Low, so a step that starts mid-stroke doesn't count a
    // partial first cycle.
    bool update(int32_t position) {
        switch (state_) {
            case Unknown:
                if (position >= high_) {
                    state_ = High;
                } else if (position <= low_) {
                    state_ = Low;
                    armed_ = true;
                }
                return false;
            case Low:
                if (position >= high_) {
                    state_ = High;
                }
                return false;
            case High:
                if (position <= low_) {
                    state_ = Low;
                    const bool completed = armed_;
                    armed_ = true;
                    return completed;
                }
                return false;
        }
        return false;
    }

    int64_t lowThreshold() const { return low_; }
    int64_t highThreshold() const { return high_; }

private:
    enum Phase : uint8_t { Unknown, Low, High };
    int64_t low_ = 0;
    int64_t high_ = 0;
    Phase state_ = Unknown;
    bool armed_ = false; // the position has been Low since start()
};
