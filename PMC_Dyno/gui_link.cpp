#include "gui_link.h"
#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>
#include "command_flags.h"
#include "data_buffer.h"

namespace {

struct OutFrame {
    uint8_t bytes[MAX_FRAME_SIZE];
    size_t length;
};

constexpr size_t OUT_QUEUE_DEPTH = 8;
// A run is sent as one GUI_send_sequence per step plus GUI_run_start, all at
// once, so the queue has to hold a whole sequence.
constexpr size_t COMMAND_QUEUE_DEPTH = 40;
constexpr uint32_t TASK_STACK = 4096;

} // namespace

void GuiLink::begin(PointBuffer* points, uint32_t baud, int core, unsigned priority) {
    points_ = points;
    Serial.begin(baud);
    outQueue_ = xQueueCreate(OUT_QUEUE_DEPTH, sizeof(OutFrame));
    commandQueue_ = xQueueCreate(COMMAND_QUEUE_DEPTH, sizeof(Command));
    xTaskCreatePinnedToCore(sendTaskFn, "gui_send", TASK_STACK, this, priority, nullptr, core);
    xTaskCreatePinnedToCore(recvTaskFn, "gui_recv", TASK_STACK, this, priority, nullptr, core);
}

bool GuiLink::send(uint8_t flag, const uint8_t* payload, size_t size) {
    OutFrame frame;
    frame.length = encodeCommandFrame(flag, payload, size, frame.bytes, sizeof(frame.bytes));
    return frame.length > 0 && xQueueSend((QueueHandle_t)outQueue_, &frame, 0) == pdTRUE;
}

void GuiLink::flushPoints(uint8_t thenFlag) {
    afterFlushFlag_ = thenFlag; // set before the request so the send task sees both
    flushRequested_ = true;
}

bool GuiLink::popCommand(Command& out) {
    return xQueueReceive((QueueHandle_t)commandQueue_, &out, 0) == pdTRUE;
}

void GuiLink::sendTaskFn(void* self) {
    ((GuiLink*)self)->sendLoop();
}

void GuiLink::recvTaskFn(void* self) {
    ((GuiLink*)self)->recvLoop();
}

void GuiLink::sendLoop() {
    OutFrame frame;
    Point points[MAX_POINTS_PER_FRAME];
    uint8_t dataFrame[MAX_FRAME_SIZE];

    for (;;) {
        // Commands first: they are rare and the GUI waits on some of them.
        if (xQueueReceive((QueueHandle_t)outQueue_, &frame, 0) == pdTRUE) {
            Serial.write(frame.bytes, frame.length);
            continue;
        }

        // Then points: full frames, or everything left when a flush was asked.
        const bool flushing = flushRequested_;
        const size_t count = points_->take(points, flushing);
        if (count > 0) {
            const size_t length = encodeDataFrame(points, count, dataFrame, sizeof(dataFrame));
            Serial.write(dataFrame, length);
            continue;
        }
        if (flushing) {
            // Every point is out: now the command that had to come after them.
            flushRequested_ = false;
            const uint8_t flag = afterFlushFlag_;
            afterFlushFlag_ = 0;
            if (flag != 0) {
                frame.length = encodeCommandFrame(flag, nullptr, 0, frame.bytes, sizeof(frame.bytes));
                Serial.write(frame.bytes, frame.length);
            }
            continue;
        }
        vTaskDelay(pdMS_TO_TICKS(1)); // nothing to send
    }
}

void GuiLink::recvLoop() {
    FrameDecoder decoder;
    Command command;

    for (;;) {
        while (Serial.available() > 0) {
            if (decoder.feed((uint8_t)Serial.read(), command) &&
                xQueueSend((QueueHandle_t)commandQueue_, &command, 0) != pdTRUE) {
                sendf(Cmd::ESP_error, "Command %u dropped: too many waiting", (unsigned)command.flag);
            }
        }
        vTaskDelay(pdMS_TO_TICKS(1));
    }
}
