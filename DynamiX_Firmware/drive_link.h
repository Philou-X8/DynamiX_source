#pragma once
#include <stdint.h>
#include <stddef.h>

// RS-485 link to the motor drive (through the conversion card) on UART2,
// Modbus RTU, 8E1. Transport only: MotorDrive (motor_drive.h) knows what the
// frames mean.
//
// One FreeRTOS task owns the UART:
//   - frames queued with sendFrame() are sent one at a time, at least
//     DRIVE_TX_GAP_MS apart, with the DE/RE pin switched around each one, so
//     the main loop never waits ~9 ms for an 8-byte frame at 9600 baud;
//   - bytes coming back are cut into frames by the Modbus idle gap and queued
//     for popResponse(). Answers can come late or never, hence the queue.

constexpr size_t DRIVE_MAX_FRAME = 32;

// Minimum time between the start of two frames: the drive's answer (~9 ms for
// 8 bytes) plus its turnaround.
constexpr uint32_t DRIVE_TX_GAP_MS = 100;

struct DriveFrame {
    uint8_t bytes[DRIVE_MAX_FRAME];
    size_t length;
};

class DriveLink {
public:
    // Configures UART2 and the DE/RE pin (receive mode) and starts the task.
    void begin(int8_t rxPin, int8_t txPin, int8_t deRePin, uint32_t baud, int core, unsigned priority);

    // Queues a complete Modbus frame (CRC included). The frame is checked
    // first (4..DRIVE_MAX_FRAME bytes, valid CRC), so a corrupted frame never
    // reaches the drive. Returns false if it was rejected or the queue is full.
    //
    // 'urgent' (for stops) first throws away frames still waiting, so this
    // one can't be refused for lack of room. A frame already on the wire
    // finishes first.
    bool sendFrame(const uint8_t* frame, size_t length, bool urgent = false);

    // Takes one frame the drive sent back. Non-blocking.
    bool popResponse(DriveFrame& out);

private:
    static void taskFn(void* self);
    void run();

    int8_t deRePin_ = -1;
    void* txQueue_ = nullptr; // QueueHandle_t of DriveFrame
    void* rxQueue_ = nullptr; // QueueHandle_t of DriveFrame
};
