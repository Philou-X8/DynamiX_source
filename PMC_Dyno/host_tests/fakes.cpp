// Link-time stand-ins for GuiLink, DriveLink and PointBuffer, which use
// FreeRTOS and the UARTs on the ESP32.
#include "fakes.h"
#include "../data_buffer.h"
#include "../drive_protocol.h"
#include "../gui_link.h"

std::vector<GuiEvent> g_guiEvents;
std::vector<Point> g_pushedPoints;
uint32_t g_pointsDropped = 0;
std::function<void(const uint8_t*, size_t, bool)> g_onDriveFrame;
std::deque<DriveFrame> g_driveReplies;

void resetFakes() {
    g_guiEvents.clear();
    g_pushedPoints.clear();
    g_pointsDropped = 0;
    g_onDriveFrame = nullptr;
    g_driveReplies.clear();
}

bool GuiLink::send(uint8_t flag, const uint8_t* payload, size_t size) {
    GuiEvent event{GuiEvent::Command, flag, {}};
    if (size > 0) {
        event.payload.assign(payload, payload + size);
    }
    g_guiEvents.push_back(event);
    return true;
}

void GuiLink::flushPoints(uint8_t thenFlag) {
    g_guiEvents.push_back(GuiEvent{GuiEvent::Flush, thenFlag, {}});
}

bool DriveLink::sendFrame(const uint8_t* frame, size_t length, bool urgent) {
    if (length < 4 || length > DRIVE_MAX_FRAME || !modbusFrameCrcOk(frame, length)) {
        return false;
    }
    if (g_onDriveFrame) {
        g_onDriveFrame(frame, length, urgent);
    }
    return true;
}

bool DriveLink::popResponse(DriveFrame& out) {
    if (g_driveReplies.empty()) {
        return false;
    }
    out = g_driveReplies.front();
    g_driveReplies.pop_front();
    return true;
}

void PointBuffer::begin() {}

bool PointBuffer::push(const Point& p) {
    g_pushedPoints.push_back(p);
    return true;
}

size_t PointBuffer::take(Point*, bool) {
    return 0;
}

void PointBuffer::reset() {
    g_pushedPoints.clear();
    dropped_ = 0;
}
