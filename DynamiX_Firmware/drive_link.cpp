#include "drive_link.h"
#include <Arduino.h>
#include <string.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>
#include "drive_protocol.h"

namespace {

constexpr size_t TX_QUEUE_DEPTH = 6;
constexpr size_t RX_QUEUE_DEPTH = 6;
constexpr uint32_t TASK_STACK = 4096;

// Modbus RTU ends a frame after 3.5 characters of silence: 3.5 * 11 bits
// (8E1) / 9600 baud = ~4 ms. 5 ms leaves room for the 1 ms polling.
constexpr uint32_t FRAME_GAP_MS = 5;

} // namespace

void DriveLink::begin(int8_t rxPin, int8_t txPin, int8_t deRePin, uint32_t baud, int core, unsigned priority) {
    deRePin_ = deRePin;
    pinMode(deRePin_, OUTPUT);
    digitalWrite(deRePin_, LOW); // receive
    Serial2.begin(baud, SERIAL_8E1, rxPin, txPin);
    txQueue_ = xQueueCreate(TX_QUEUE_DEPTH, sizeof(DriveFrame));
    rxQueue_ = xQueueCreate(RX_QUEUE_DEPTH, sizeof(DriveFrame));
    xTaskCreatePinnedToCore(taskFn, "drive_link", TASK_STACK, this, priority, nullptr, core);
}

bool DriveLink::sendFrame(const uint8_t* frame, size_t length, bool urgent) {
    if (length < 4 || length > DRIVE_MAX_FRAME || !modbusFrameCrcOk(frame, length)) {
        return false;
    }
    DriveFrame msg;
    msg.length = length;
    memcpy(msg.bytes, frame, length);
    if (urgent) {
        xQueueReset((QueueHandle_t)txQueue_);
    }
    return xQueueSend((QueueHandle_t)txQueue_, &msg, 0) == pdTRUE;
}

bool DriveLink::popResponse(DriveFrame& out) {
    return xQueueReceive((QueueHandle_t)rxQueue_, &out, 0) == pdTRUE;
}

void DriveLink::taskFn(void* self) {
    ((DriveLink*)self)->run();
}

void DriveLink::run() {
    DriveFrame rx;
    rx.length = 0;
    uint32_t lastByteMs = 0;
    uint32_t lastTxMs = millis() - DRIVE_TX_GAP_MS; // the first frame goes out right away

    for (;;) {
        // Send the next frame once the gap has passed and no answer is coming
        // in: DE/RE high, write, wait for the last bit, back to receive.
        DriveFrame tx;
        if (rx.length == 0 && millis() - lastTxMs >= DRIVE_TX_GAP_MS &&
            xQueueReceive((QueueHandle_t)txQueue_, &tx, 0) == pdTRUE) {
            digitalWrite(deRePin_, HIGH);
            delayMicroseconds(10);
            Serial2.write(tx.bytes, tx.length);
            Serial2.flush();
            delayMicroseconds(10);
            digitalWrite(deRePin_, LOW);
            lastTxMs = millis();
        }

        // Collect the answer; bytes past DRIVE_MAX_FRAME are dropped.
        while (Serial2.available() > 0) {
            const uint8_t b = (uint8_t)Serial2.read();
            if (rx.length < DRIVE_MAX_FRAME) {
                rx.bytes[rx.length++] = b;
            }
            lastByteMs = millis();
        }

        // Silence after at least one byte: the frame is complete.
        if (rx.length > 0 && millis() - lastByteMs >= FRAME_GAP_MS) {
            xQueueSend((QueueHandle_t)rxQueue_, &rx, 0); // dropped if nobody reads them
            rx.length = 0;
        }

        vTaskDelay(pdMS_TO_TICKS(1));
    }
}
