#pragma once
#include <cmath>
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

// The static force (preload and gas spring: what the shock pushes with at rest) from one
// slow cycle, as a straight line through the stroke. It is NOT the line between the forces
// at the two ends: the ends are biased by seal friction and bushing play, and the force
// is flat through the middle. Like the team's HMI, the line comes from
//   - the level: the mean of the forces at the two dead centres, where the speed is near 0
//     and changes sign (the damping force cancels, averaged on both sides of each end);
//   - the slope: least-squares fits of force against position through the middle of the
//     stroke (20 to 80 %), one on the compression branch and one on the rebound branch;
//     the static force lies midway between them, so its slope is the mean of the two.
// Feed it every sample (spike-filtered) while the yoke turns slowly. It starts counting at
// the first complete pass through a dead centre and is done when the yoke is back at that
// end: exactly one cycle, so both branches are seen over their whole length.
class StaticForceFit {
public:
    // bottomIsHigh: the bottom of the stroke is the highest position. endWindowPermille: the
    // dead-centre windows, in 0.1 % of the stroke. hysteresis: how far the position has to
    // come back before the direction counts as reversed (position counts).
    void start(int32_t strokeMin, int32_t strokeMax, bool bottomIsHigh, int32_t endWindowPermille, int32_t hysteresis) {
        min_ = strokeMin;
        max_ = strokeMax;
        bottomIsHigh_ = bottomIsHigh;
        window_ = endWindowPermille;
        hysteresis_ = hysteresis;
        direction_ = 0;
        hasValue_ = false;
        complete_ = false;
        firstEnd_ = -1;
        lo_ = hi_ = 0;
        for (Branch& b : branch_) {
            b = Branch();
        }
        for (End& e : end_) {
            e = End();
        }
    }

    // True once the fit is complete.
    bool update(int32_t position, int32_t force) {
        const int64_t span = (int64_t)max_ - min_;
        if (span <= 0 || complete_) {
            return complete_;
        }
        const int32_t x = bottomIsHigh_ ? max_ - position : position - min_; // counts above the bottom
        const int32_t p = (int32_t)((int64_t)x * 1000 / span);                // 0.1 % of the stroke
        if (!hasValue_) {
            forceRef_ = force;
        }
        trackDirection(x);

        // the middle of the stroke: the branch fits, over the cycle that starts at the first dead centre
        if (firstEnd_ >= 0 && direction_ != 0 && p >= 200 && p <= 800) {
            Branch& b = branch_[direction_ > 0 ? 0 : 1];
            const double dx = (double)x - span / 2.0;
            const double dy = (double)force - forceRef_;
            b.n++;
            b.sx += dx;
            b.sy += dy;
            b.sxx += dx * dx;
            b.sxy += dx * dy;
            if (x < b.xMin) b.xMin = x;
            if (x > b.xMax) b.xMax = x;
        }

        // the dead centres: one complete pass through each window, entered from outside
        const bool passed0 = updateEnd(end_[0], p <= window_, p > window_ + window_ / 2, force, !hasValue_);
        const bool passed1 = updateEnd(end_[1], p >= 1000 - window_, p < 1000 - window_ - window_ / 2, force, !hasValue_);
        if (firstEnd_ < 0 && (passed0 || passed1)) {
            firstEnd_ = passed0 ? 0 : 1;
            for (Branch& b : branch_) { // the cycle starts here
                b = Branch();
            }
        }
        hasValue_ = true;
        if (firstEnd_ >= 0 && end_[firstEnd_].passes >= 2 && end_[1 - firstEnd_].passes >= 1 &&
            covers(branch_[0], span) && covers(branch_[1], span)) {
            complete_ = true;
        }
        return complete_;
    }

    bool done() const { return complete_; }

    // The static force line, as force counts at the bottom and at the top of the stroke.
    int32_t bottomLine() const { return (int32_t)std::llround(level() - slope() * span() / 2.0); }
    int32_t topLine() const { return (int32_t)std::llround(level() + slope() * span() / 2.0); }
    // Force counts per position count, + = the force grows towards the top of the stroke.
    double slope() const {
        double slopes[2];
        for (int i = 0; i < 2; i++) {
            const Branch& b = branch_[i];
            const double d = b.n * b.sxx - b.sx * b.sx;
            if (b.n < MIN_BRANCH_SAMPLES || d < 1e-9) {
                return endSlope(); // too few points in the middle: fall back on the dead centres
            }
            slopes[i] = (b.n * b.sxy - b.sx * b.sy) / d;
        }
        return (slopes[0] + slopes[1]) / 2.0;
    }
    uint32_t endSamples() const { return end_[0].n + end_[1].n; }
    uint32_t branchSamples() const { return branch_[0].n + branch_[1].n; }

private:
    static constexpr uint32_t MIN_BRANCH_SAMPLES = 20;

    struct Branch {
        uint32_t n = 0;
        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        int32_t xMin = INT32_MAX;
        int32_t xMax = INT32_MIN;
    };
    struct End {
        bool inside = false;
        bool partial = false; // started inside the window: a pass that doesn't count
        uint32_t passes = 0;  // complete passes through the window
        int64_t curSum = 0;
        uint32_t curN = 0;
        int64_t sum = 0;      // over the complete passes
        uint32_t n = 0;
    };

    double span() const { return (double)((int64_t)max_ - min_); }
    double mean(const End& e) const { return e.n == 0 ? 0.0 : (double)e.sum / e.n; }
    double level() const { return (mean(end_[0]) + mean(end_[1])) / 2.0; }
    double endSlope() const { return span() > 0 ? (mean(end_[1]) - mean(end_[0])) / span() : 0.0; }

    // Seen over the middle of the stroke: from the first quarter to the last quarter.
    static bool covers(const Branch& b, int64_t span) {
        return b.n >= MIN_BRANCH_SAMPLES && (int64_t)b.xMin * 1000 <= 250 * span && (int64_t)b.xMax * 1000 >= 750 * span;
    }

    void trackDirection(int32_t x) {
        if (!hasValue_) {
            lo_ = hi_ = x;
            return;
        }
        if (direction_ == 0) {
            if (x < lo_) lo_ = x;
            if (x > hi_) hi_ = x;
            if (x - lo_ > hysteresis_) {
                direction_ = 1;
                hi_ = x;
            } else if (hi_ - x > hysteresis_) {
                direction_ = -1;
                lo_ = x;
            }
        } else if (direction_ > 0) {
            if (x > hi_) {
                hi_ = x;
            } else if (hi_ - x > hysteresis_) {
                direction_ = -1;
                lo_ = x;
            }
        } else {
            if (x < lo_) {
                lo_ = x;
            } else if (x - lo_ > hysteresis_) {
                direction_ = 1;
                hi_ = x;
            }
        }
    }

    // inWindow: this sample is within the window; outside: far enough out to have left it.
    // True on the sample that completes a pass.
    static bool updateEnd(End& e, bool inWindow, bool outside, int32_t force, bool first) {
        if (!e.inside) {
            if (!inWindow) {
                return false;
            }
            e.inside = true;
            e.partial = first; // the fit started in the window: only part of the pass is seen
            e.curSum = 0;
            e.curN = 0;
        }
        if (inWindow && !e.partial) {
            e.curSum += force;
            e.curN++;
        }
        if (!outside) {
            return false;
        }
        e.inside = false;
        if (e.partial || e.curN == 0) {
            return false;
        }
        e.sum += e.curSum;
        e.n += e.curN;
        e.passes++;
        return true;
    }

    int32_t min_ = 0;
    int32_t max_ = 0;
    bool bottomIsHigh_ = false;
    int32_t window_ = 0;
    int32_t hysteresis_ = 0;
    int32_t direction_ = 0; // +1 towards the top of the stroke, -1 towards the bottom, 0 not known yet
    bool hasValue_ = false;
    bool complete_ = false;
    int32_t firstEnd_ = -1; // the end (0 bottom, 1 top) whose first complete pass started the cycle
    int32_t lo_ = 0;
    int32_t hi_ = 0;
    int32_t forceRef_ = 0;
    Branch branch_[2];      // 0 = compression (towards the top), 1 = rebound
    End end_[2];            // 0 = bottom, 1 = top
};
