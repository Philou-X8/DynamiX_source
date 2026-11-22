#pragma once
#include <stdint.h>

// Building blocks for the sequences that move the yoke to a known place or
// measure the load at a known place (TAR, bottom out, run calibration).
// Header-only and free of Arduino dependencies, so the host tests check them.
// Feed them spike-filtered values (Median5).

// Where 'position' is along the stroke, in 0.1 %, counted towards one end:
// 0 = the other end, 1000 = 'end'. endIsHigh: 'end' is the highest position.
inline int32_t strokePermille(int32_t position, int32_t strokeMin, int32_t strokeMax, bool endIsHigh) {
    const int64_t span = (int64_t)strokeMax - strokeMin;
    if (span <= 0) {
        return 0;
    }
    const int32_t fromMin = (int32_t)(((int64_t)position - strokeMin) * 1000 / span);
    return endIsHigh ? fromMin : 1000 - fromMin;
}

// The force with nothing moving: waits 'settleMs', then averages 'samples'.
class RestForce {
public:
    void start(uint32_t nowMs, uint32_t settleMs, uint32_t samples) {
        startMs_ = nowMs;
        settleMs_ = settleMs;
        samples_ = samples;
        sum_ = 0;
        count_ = 0;
    }

    // True once the average is complete.
    bool update(int32_t force, uint32_t nowMs) {
        if (nowMs - startMs_ < settleMs_) {
            return false;
        }
        if (count_ < samples_) {
            sum_ += force;
            count_++;
        }
        return count_ >= samples_;
    }

    int32_t value() const { return count_ == 0 ? 0 : (int32_t)(sum_ / (int64_t)count_); }

private:
    uint32_t startMs_ = 0;
    uint32_t settleMs_ = 0;
    uint32_t samples_ = 0;
    int64_t sum_ = 0;
    uint32_t count_ = 0;
};

// Fires once when the yoke, on its way to one end of the stroke, passes a
// point: it must first be seen in the other half of the stroke, so a yoke that
// starts past the point (or wobbles around it) doesn't fire early.
class EndApproach {
public:
    // permille: the point, in 0.1 % of the stroke counted towards 'end' (1000 = the end).
    void start(int32_t strokeMin, int32_t strokeMax, bool endIsHigh, int32_t permille) {
        min_ = strokeMin;
        max_ = strokeMax;
        endIsHigh_ = endIsHigh;
        permille_ = permille;
        armed_ = false;
    }

    // True on the sample that passes the point.
    bool update(int32_t position) {
        const int32_t p = strokePermille(position, min_, max_, endIsHigh_);
        if (!armed_) {
            armed_ = p <= 500;
            return false;
        }
        if (p >= permille_) {
            armed_ = false; // once
            return true;
        }
        return false;
    }

private:
    int32_t min_ = 0;
    int32_t max_ = 0;
    bool endIsHigh_ = true;
    int32_t permille_ = 1000;
    bool armed_ = false;
};

// Finds the stroke from the turning points of one cycle: the yoke must turn
// back at both ends (a high turn and a low turn, in either order). A turn
// counts once the position has come back by 'hysteresis' from its extreme,
// so noise can't make one. Started near an end, this takes a little over one
// cycle; a stroke narrower than 'minRange' is ignored (keeps looking).
class TurnStrokeFinder {
public:
    void start(int32_t hysteresis, int32_t minRange) {
        hysteresis_ = hysteresis;
        minRange_ = minRange;
        direction_ = Direction::Unknown;
        hasValue_ = false;
        haveHighTurn_ = false;
        haveLowTurn_ = false;
    }

    void update(int32_t position) {
        if (!hasValue_) {
            low_ = high_ = position;
            hasValue_ = true;
            return;
        }
        switch (direction_) {
            case Direction::Unknown: // no turn possible yet: just find which way it goes
                if (position < low_) low_ = position;
                if (position > high_) high_ = position;
                if (position - low_ > hysteresis_) {
                    direction_ = Direction::Rising;
                    high_ = position;
                } else if (high_ - position > hysteresis_) {
                    direction_ = Direction::Falling;
                    low_ = position;
                }
                return;
            case Direction::Rising:
                if (position > high_) {
                    high_ = position;
                } else if (high_ - position > hysteresis_) {
                    highTurn_ = high_;
                    haveHighTurn_ = true;
                    direction_ = Direction::Falling;
                    low_ = position;
                }
                return;
            case Direction::Falling:
                if (position < low_) {
                    low_ = position;
                } else if (position - low_ > hysteresis_) {
                    lowTurn_ = low_;
                    haveLowTurn_ = true;
                    direction_ = Direction::Rising;
                    high_ = position;
                }
                return;
        }
    }

    bool found() const {
        return haveHighTurn_ && haveLowTurn_ && (int64_t)highTurn_ - lowTurn_ >= minRange_;
    }
    int32_t min() const { return lowTurn_; }
    int32_t max() const { return highTurn_; }

private:
    enum class Direction : uint8_t { Unknown, Rising, Falling };
    int32_t hysteresis_ = 0;
    int32_t minRange_ = 0;
    Direction direction_ = Direction::Unknown;
    bool hasValue_ = false;
    int32_t low_ = 0;   // extreme of the current half-cycle
    int32_t high_ = 0;
    bool haveHighTurn_ = false;
    bool haveLowTurn_ = false;
    int32_t highTurn_ = 0;
    int32_t lowTurn_ = 0;
};

// Average force while the yoke goes through one end of the stroke: every
// sample within 'windowPermille' of the end, from entering the window to
// leaving it again. The window is symmetric around the dead centre, where the
// speed is near 0 and changes sign, so what is left of the damping force
// cancels out and only the static force remains. Leaving needs to be past the
// window by half its width again, so noise at the edge can't end it early.
class EndForceAverage {
public:
    void start(int32_t strokeMin, int32_t strokeMax, bool endIsHigh, int32_t windowPermille) {
        min_ = strokeMin;
        max_ = strokeMax;
        endIsHigh_ = endIsHigh;
        window_ = windowPermille;
        sum_ = 0;
        count_ = 0;
    }

    // True once the yoke has gone through the window.
    bool update(int32_t position, int32_t force) {
        const int32_t p = strokePermille(position, min_, max_, endIsHigh_);
        if (p >= 1000 - window_) {
            sum_ += force;
            count_++;
            return false;
        }
        return count_ > 0 && p < 1000 - window_ - window_ / 2;
    }

    int32_t value() const { return count_ == 0 ? 0 : (int32_t)(sum_ / (int64_t)count_); }
    uint32_t count() const { return count_; }

private:
    int32_t min_ = 0;
    int32_t max_ = 0;
    bool endIsHigh_ = true;
    int32_t window_ = 0;
    int64_t sum_ = 0;
    uint32_t count_ = 0;
};
