#include "drive_comms.h"
#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>

namespace {

constexpr size_t TX_QUEUE_DEPTH = 4;
constexpr size_t RX_QUEUE_DEPTH = 4;

// Modbus RTU ends a frame after 3.5 characters of silence. One character is
// 11 bits (8E1), so 3.5 * 11 / 9600 = ~4 ms. 5 ms leaves a margin for the
// 1 ms polling and millis() granularity.
constexpr uint32_t FRAME_GAP_MS = 5;

} // namespace

uint16_t modbusCrc16(const uint8_t* data, size_t length) {
    uint16_t crc = 0xFFFF;
    for (size_t pos = 0; pos < length; pos++) {
        crc ^= data[pos];
        for (int i = 0; i < 8; i++) {
            if (crc & 0x0001) {
                crc >>= 1;
                crc ^= 0xA001;
            } else {
                crc >>= 1;
            }
        }
    }
    return crc;
}

void DriveComms::begin(int8_t rxPin, int8_t txPin, int8_t dePin, uint32_t baud) {
    dePin_ = dePin;
    pinMode(dePin_, OUTPUT);
    digitalWrite(dePin_, LOW); // start in receive mode

    Serial2.begin(baud, SERIAL_8E1, rxPin, txPin); // UART2

    txQueue_ = xQueueCreate(TX_QUEUE_DEPTH, sizeof(DriveFrame));
    rxQueue_ = xQueueCreate(RX_QUEUE_DEPTH, sizeof(DriveFrame));
}

bool DriveComms::sendFrame(const uint8_t* frame, size_t length, bool urgent) {
    if (length < 4 || length > DRIVE_MAX_FRAME) {
        return false; // address + function + CRC is the shortest valid frame
    }
    uint16_t crc = modbusCrc16(frame, length - 2);
    if (frame[length - 2] != (crc & 0xFF) || frame[length - 1] != (crc >> 8)) {
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

bool DriveComms::popResponse(DriveFrame& out) {
    return xQueueReceive((QueueHandle_t)rxQueue_, &out, 0) == pdTRUE;
}

void DriveComms::taskFn(void* pvParameters) {
    DriveComms* self = (DriveComms*)pvParameters;

    DriveFrame rx;
    rx.length = 0;
    uint32_t lastByteMs = 0;
    uint32_t lastTxMs = millis() - DRIVE_TX_GAP_MS; // so the first frame isn't delayed

    for (;;) {
        // Transmit one queued frame, but only once the previous one has had
        // its gap and no reply is half-received. Same sequence as the
        // tested motor_drive.ino: DE/RE high, write, wait until the last
        // bit is out, back to receive.
        DriveFrame tx;
        if (rx.length == 0 && (millis() - lastTxMs) >= DRIVE_TX_GAP_MS &&
            xQueueReceive((QueueHandle_t)self->txQueue_, &tx, 0) == pdTRUE) {
            digitalWrite(self->dePin_, HIGH);
            delayMicroseconds(10);
            Serial2.write(tx.bytes, tx.length);
            Serial2.flush();
            delayMicroseconds(10);
            digitalWrite(self->dePin_, LOW);
            lastTxMs = millis();
        }

        // Collect whatever the drive sent back; bytes past the frame size
        // are dropped.
        while (Serial2.available() > 0) {
            uint8_t b = (uint8_t)Serial2.read();
            if (rx.length < DRIVE_MAX_FRAME) {
                rx.bytes[rx.length++] = b;
            }
            lastByteMs = millis();
        }

        // Silence after at least one byte means the frame is complete.
        if (rx.length > 0 && (millis() - lastByteMs) >= FRAME_GAP_MS) {
            xQueueSend((QueueHandle_t)self->rxQueue_, &rx, 0); // dropped if the queue is full
            rx.length = 0;
        }

        vTaskDelay(pdMS_TO_TICKS(1));
    }
}
