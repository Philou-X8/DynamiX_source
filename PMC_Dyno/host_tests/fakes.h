#pragma once
// Test doubles for the parts of the firmware that touch the hardware. The
// state machine and MotorDrive are compiled unchanged against these.
#include <cstdint>
#include <deque>
#include <functional>
#include <string>
#include <vector>
#include "../drive_link.h"
#include "../protocol.h"

// What the firmware sent to the GUI, in order.
struct GuiEvent {
    enum Kind { Command, Flush } kind;
    uint8_t flag;                 // command flag, or the flag sent after a flush (0 = none)
    std::vector<uint8_t> payload;
    std::string text() const { return std::string(payload.begin(), payload.end()); }
};

extern std::vector<GuiEvent> g_guiEvents;
extern std::vector<Point> g_pushedPoints;
extern uint32_t g_pointsDropped;

// Called for every frame the firmware hands to the DriveLink.
extern std::function<void(const uint8_t* frame, size_t length, bool urgent)> g_onDriveFrame;
// Answers the fake DriveLink gives back through popResponse().
extern std::deque<DriveFrame> g_driveReplies;

void resetFakes();
