#pragma once
#include <stdint.h>

// Small integer filters used on the sensor samples. Header-only and free of
// Arduino dependencies, so the host tests check them on a PC.

// Median of the last 5 samples. One or two bad readings in a row (spikes)
// can't move it, while a real change shows up within 3 samples. Before 5
// samples are in, the median of those available.
class Median5 {
public:
    void reset() { count_ = 0; next_ = 0; }

    int32_t update(int32_t value) {
        window_[next_] = value;
        next_ = (uint8_t)((next_ + 1) % 5);
        if (count_ < 5) {
            count_++;
        }
        int32_t sorted[5];
        for (uint8_t i = 0; i < count_; i++) {
            // insertion sort, 5 values at most
            int32_t v = window_[i];
            uint8_t j = i;
            while (j > 0 && sorted[j - 1] > v) {
                sorted[j] = sorted[j - 1];
                j--;
            }
            sorted[j] = v;
        }
        return sorted[count_ / 2];
    }

private:
    int32_t window_[5] = {0, 0, 0, 0, 0};
    uint8_t count_ = 0;
    uint8_t next_ = 0;
};

// Plain average of everything added since reset().
class RunningMean {
public:
    void reset() { sum_ = 0; count_ = 0; }
    void add(int32_t value) { sum_ += value; count_++; }
    uint32_t count() const { return count_; }
    int32_t mean() const { return count_ == 0 ? 0 : (int32_t)(sum_ / (int64_t)count_); }

private:
    int64_t sum_ = 0;
    uint32_t count_ = 0;
};

// Exponential average with a time constant of 32 samples (~64 ms at 500 Hz),
// for "what is the force right now" without the noise.
class SlowAverage {
public:
    int32_t update(int32_t sample) {
        const int64_t scaled = (int64_t)sample << SHIFT;
        if (!started_) {
            state_ = scaled;
            started_ = true;
        } else {
            state_ += (scaled - state_) / 32;
        }
        return value();
    }
    int32_t value() const { return (int32_t)(state_ / (1 << SHIFT)); }

private:
    static constexpr int SHIFT = 8;
    int64_t state_ = 0;
    bool started_ = false;
};

// Finds the stroke (lowest and highest position) while the motor turns:
// found once it has looked for at least 'minTimeMs', the range is at least
// 'minRange' wide, and it hasn't grown by more than 1% for 'settleMs'.
// Feed it spike-filtered positions (Median5), so one bad reading can't
// stretch the range.
class StrokeFinder {
public:
    enum class Result { Searching, Found, Failed };

    void start(uint32_t nowMs, int32_t minRange, uint32_t minTimeMs, uint32_t settleMs, uint32_t timeoutMs) {
        startMs_ = nowMs;
        lastGrowthMs_ = nowMs;
        minRange_ = minRange;
        minTimeMs_ = minTimeMs;
        settleMs_ = settleMs;
        timeoutMs_ = timeoutMs;
        hasValue_ = false;
        min_ = 0;
        max_ = 0;
    }

    void update(int32_t position, uint32_t nowMs) {
        if (!hasValue_) {
            min_ = max_ = position;
            hasValue_ = true;
            lastGrowthMs_ = nowMs;
            rangeAtLastGrowth_ = 0;
            return;
        }
        if (position < min_) min_ = position;
        if (position > max_) max_ = position;
        // Compared with the range at the last growth, not the previous sample,
        // so a slow sweep adding a little each sample still counts. Less than
        // 1% is noise at the ends, not a wider stroke.
        if (range() - rangeAtLastGrowth_ > rangeAtLastGrowth_ / 100) {
            lastGrowthMs_ = nowMs;
            rangeAtLastGrowth_ = range();
        }
    }

    Result result(uint32_t nowMs) const {
        if (hasValue_ && nowMs - startMs_ >= minTimeMs_ && range() >= minRange_ &&
            nowMs - lastGrowthMs_ >= settleMs_) {
            return Result::Found;
        }
        return nowMs - startMs_ >= timeoutMs_ ? Result::Failed : Result::Searching;
    }

    int32_t min() const { return min_; }
    int32_t max() const { return max_; }
    int64_t range() const { return (int64_t)max_ - (int64_t)min_; }

private:
    uint32_t startMs_ = 0;
    uint32_t lastGrowthMs_ = 0;
    int32_t minRange_ = 0;
    uint32_t minTimeMs_ = 0;
    uint32_t settleMs_ = 0;
    uint32_t timeoutMs_ = 0;
    bool hasValue_ = false;
    int32_t min_ = 0;
    int32_t max_ = 0;
    int64_t rangeAtLastGrowth_ = 0;
};

// Counts how many times in a row a condition held; for "N samples in a row
// over the limit" checks.
class InARow {
public:
    // Returns true once 'condition' has held 'needed' times in a row.
    bool update(bool condition, uint32_t needed) {
        count_ = condition ? count_ + 1 : 0;
        return count_ >= needed;
    }
    void reset() { count_ = 0; }

private:
    uint32_t count_ = 0;
};
