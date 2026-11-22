#pragma once
#include <stdint.h>
#include <stddef.h>
#include <stdarg.h>
#include <stdio.h>
#include <string.h>
#include "printf_format.h"
#include "protocol.h"

class PointBuffer;

// USB serial link to the GUI (UART0). Two FreeRTOS tasks own the port:
//   - send task: writes queued commands first (acks, messages, errors), then
//     run points from the PointBuffer, one data frame at a time.
//   - receive task: decodes incoming frames and queues the commands for the
//     main loop, so every state change happens on the main loop's thread.
//
// All public functions are safe to call from any task.
class GuiLink {
public:
    // Opens the serial port and starts both tasks on 'core'.
    void begin(PointBuffer* points, uint32_t baud, int core, unsigned priority);

    // Queues a command for the GUI ('payload' may be null when size is 0).
    // Returns false if it doesn't fit or the queue is full.
    bool send(uint8_t flag, const uint8_t* payload = nullptr, size_t size = 0);

    bool sendText(uint8_t flag, const char* text) {
        return send(flag, (const uint8_t*)text, strlen(text));
    }

    // printf-style text command, cut to the payload size.
    bool sendf(uint8_t flag, const char* fmt, ...) PRINTF_FORMAT(3, 4);
    bool sendv(uint8_t flag, const char* fmt, va_list args);

    // Asks the send task to send every point still waiting, even a partial
    // frame (normally only full frames go out), then 'thenFlag' if it isn't 0.
    // Used at the end of a run so ESP_run_finished arrives after the last point.
    void flushPoints(uint8_t thenFlag = 0);

    // Main loop side: takes the next command received from the GUI. Non-blocking.
    bool popCommand(Command& out);

private:
    static void sendTaskFn(void* self);
    static void recvTaskFn(void* self);
    void sendLoop();
    void recvLoop();

    PointBuffer* points_ = nullptr;
    void* outQueue_ = nullptr;     // QueueHandle_t of outgoing command frames
    void* commandQueue_ = nullptr; // QueueHandle_t of received Commands
    volatile bool flushRequested_ = false;
    volatile uint8_t afterFlushFlag_ = 0;
};

inline bool GuiLink::sendv(uint8_t flag, const char* fmt, va_list args) {
    char text[MAX_COMMAND_PAYLOAD + 1];
    if (vsnprintf(text, sizeof(text), fmt, args) < 0) {
        return false;
    }
    return send(flag, (const uint8_t*)text, strlen(text));
}

inline bool GuiLink::sendf(uint8_t flag, const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    const bool sent = sendv(flag, fmt, args);
    va_end(args);
    return sent;
}
