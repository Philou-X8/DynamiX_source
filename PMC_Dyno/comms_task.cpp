#include "comms_task.h"
#include <Arduino.h>
#include <string.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>

namespace {

struct PriorityMsg {
    uint8_t bytes[MAX_FRAME_SIZE];
    size_t length;
};

constexpr size_t PRIORITY_QUEUE_DEPTH = 4;
constexpr size_t INSTRUCTION_QUEUE_DEPTH = 8;

} // namespace

void CommsTask::begin(DoubleBuffer* dataBuffer, StateMachine* stateMachine, uint32_t baudRate) {
    dataBuffer_ = dataBuffer;
    stateMachine_ = stateMachine;

    Serial.begin(baudRate); // 115200 per design doc section 5; revisit if
                              // headroom proves too tight (see doc's
                              // fallback options).

    priorityQueueHandle_ = xQueueCreate(PRIORITY_QUEUE_DEPTH, sizeof(PriorityMsg));
    instructionQueueHandle_ = xQueueCreate(INSTRUCTION_QUEUE_DEPTH, sizeof(Instruction));
}

bool CommsTask::enqueuePriorityMessage(const uint8_t* payload, size_t payloadSize) {
    uint8_t frame[MAX_FRAME_SIZE];
    size_t frameLen = encodeInstructionFrame(payload, payloadSize, frame, sizeof(frame));
    if (frameLen == 0) {
        return false;
    }
    PriorityMsg msg;
    msg.length = frameLen;
    memcpy(msg.bytes, frame, frameLen);
    return xQueueSend((QueueHandle_t)priorityQueueHandle_, &msg, 0) == pdTRUE;
}

bool CommsTask::popInstruction(Instruction& outInstr) {
    return xQueueReceive((QueueHandle_t)instructionQueueHandle_, &outInstr, 0) == pdTRUE;
}

void CommsTask::sendTaskFn(void* pvParameters) {
    CommsTask* self = (CommsTask*)pvParameters;

    uint8_t frameBuf[MAX_FRAME_SIZE];
    Point pointBuf[MAX_POINTS_PER_BUFFER];

    for (;;) {
        // Priority traffic first (acks/errors/heartbeats) -- rare, allowed
        // to be blocking per design doc section 3/5.
        PriorityMsg pmsg;
        if (xQueueReceive((QueueHandle_t)self->priorityQueueHandle_, &pmsg, 0) == pdTRUE) {
            Serial.write(pmsg.bytes, pmsg.length);
            continue;
        }

        // Otherwise, data path: send once the write buffer is full.
        // TODO: also call dataBuffer_->forceSwap(...) here on a
        // state-machine-exit signal, per design doc open item 2, instead
        // of only ever swapping on "full".
        size_t count = 0;
        if (self->dataBuffer_->trySwap(pointBuf, count)) {
            size_t frameLen = encodeDataFrame(pointBuf, count, frameBuf, sizeof(frameBuf));
            if (frameLen > 0) {
                Serial.write(frameBuf, frameLen);
            }
        } else {
            vTaskDelay(pdMS_TO_TICKS(1)); // nothing to do; avoid busy-spin
        }
    }
}

void CommsTask::recvTaskFn(void* pvParameters) {
    CommsTask* self = (CommsTask*)pvParameters;
    FrameDecoder decoder;
    DecodedFrame frame;

    for (;;) {
        while (Serial.available() > 0) {
            uint8_t b = (uint8_t)Serial.read();
            if (decoder.feed(b, frame)) {
                if (frame.type == FrameType::Instruction) {
                    Instruction instr;
                    instr.size = frame.payloadSize < sizeof(instr.raw)
                                     ? frame.payloadSize
                                     : sizeof(instr.raw);
                    memcpy(instr.raw, frame.payload, instr.size);
                    // Handed off to the main loop via queue rather than
                    // calling stateMachine_ directly from this task, so
                    // all state transitions stay owned by one thread.
                    xQueueSend((QueueHandle_t)self->instructionQueueHandle_, &instr, 0);
                }
                // Frames typed Data arriving from the PC would be
                // unexpected (PC only sends instructions) -- ignored for
                // now; could be logged as a protocol error if that comes
                // up in practice.
            }
        }
        vTaskDelay(pdMS_TO_TICKS(1));
    }
}
