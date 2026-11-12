#pragma once
#include <stdint.h>
#include <stddef.h>

// Serial link to the motor drive: RS-485 (through the conversion card) on
// UART2, Modbus RTU, 9600 baud, 8E1. See the TD350 manual, chapter 9.
//
// One FreeRTOS task (taskFn) owns the UART, mirroring how CommsTask owns the
// USB link:
//   - sendFrame() queues a complete Modbus frame (CRC included). The task
//     drives the DE/RE pin and transmits it, so the main loop never blocks
//     for the ~9 ms an 8-byte frame takes at 9600 baud.
//   - Bytes coming back are grouped into frames by the Modbus idle gap
//     (3.5 characters) and queued for popResponse(), which the main loop
//     calls non-blocking, the same way it calls CommsTask::popInstruction().
//
// The drive's answers can arrive late (or never), which is why they go
// through a queue instead of being waited on.

constexpr size_t DRIVE_MAX_FRAME = 32;

// Minimum time between the start of one transmitted frame and the next.
// Covers the drive's reply (~9 ms for 8 bytes at 9600 baud) plus its turnaround.
constexpr uint32_t DRIVE_TX_GAP_MS = 100;

struct DriveFrame {
    uint8_t bytes[DRIVE_MAX_FRAME];
    size_t length;
};

// Modbus RTU CRC-16 (poly 0xA001). In a frame it is sent low byte first.
uint16_t modbusCrc16(const uint8_t* data, size_t length);

class DriveComms {
public:
    // Configure the UART and the DE/RE direction pin (starts in receive mode).
    void begin(int8_t rxPin, int8_t txPin, int8_t dePin, uint32_t baud = 9600);

    // Queue a complete Modbus frame for transmission. The frame is checked
    // first (4..DRIVE_MAX_FRAME bytes, valid CRC) so a corrupted or malformed
    // frame never reaches the drive. Returns false if it was rejected or the
    // send queue is full. Non-blocking.
    //
    // 'urgent' (for stops) first discards any frames still waiting in the
    // send queue so this one can't be dropped for lack of room. A frame
    // already on the wire is not interrupted.
    //
    // Consecutive frames are spaced by DRIVE_TX_GAP_MS so the drive has time
    // to answer (and to see a clean idle gap) before the next one starts.
    bool sendFrame(const uint8_t* frame, size_t length, bool urgent = false);

    // Pops one frame the drive sent back. Returns false when there is none.
    // Non-blocking, call it from the main loop.
    bool popResponse(DriveFrame& out);

    // FreeRTOS task entry point (launched via xTaskCreatePinnedToCore in the
    // main sketch, with a DriveComms* as parameter).
    static void taskFn(void* pvParameters);

private:
    int8_t dePin_ = -1;
    void* txQueue_ = nullptr; // QueueHandle_t, opaque here
    void* rxQueue_ = nullptr; // QueueHandle_t, opaque here
};
