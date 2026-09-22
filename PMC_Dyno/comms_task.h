#pragma once
#include <stdint.h>
#include <stddef.h>
#include "protocol.h"
#include "data_buffer.h"
#include "state_machine.h"

// Owns the UART. Two FreeRTOS tasks:
//   - sendTaskFn: waits for the double buffer to be ready (full, or
//     force-flushed on state exit), swaps it, frames it, and writes it out.
//     Before pulling the next data chunk it always drains the priority
//     queue first (acks/errors/heartbeats) -- see design doc section 3/5.
//   - recvTaskFn: reads incoming bytes, feeds them to a FrameDecoder, and
//     pushes decoded instruction frames onto the instruction queue for the
//     main loop / state machine to consume.
//
// Priority traffic is expected to be rare and is allowed to block briefly
// (finish whatever's currently on the wire, then send) per the design
// doc's Q2 discussion -- no need for anything fancier than a small queue.
class CommsTask {
public:
    void begin(DoubleBuffer* dataBuffer, StateMachine* stateMachine, uint32_t baudRate);

    // Enqueue a pre-built instruction-type payload (ack/error/heartbeat)
    // for the send task to prioritize ahead of the next data chunk.
    // Returns false if the priority queue is full.
    bool enqueuePriorityMessage(const uint8_t* payload, size_t payloadSize);

    // Called from the main loop (not the recv task) so all state
    // transitions stay owned by a single thread. Returns true and fills
    // outInstr if one was pending; non-blocking.
    bool popInstruction(Instruction& outInstr);

    // FreeRTOS task entry points (launched via xTaskCreatePinnedToCore in
    // main.ino).
    static void sendTaskFn(void* pvParameters);
    static void recvTaskFn(void* pvParameters);

private:
    DoubleBuffer* dataBuffer_ = nullptr;
    StateMachine* stateMachine_ = nullptr;

    void* priorityQueueHandle_ = nullptr;   // QueueHandle_t, opaque here
    void* instructionQueueHandle_ = nullptr; // QueueHandle_t, opaque here

    // TODO: fixed-size element type for the priority queue (frame bytes +
    // length) and for the instruction queue (Instruction, from
    // state_machine.h) once instruction payload formats are finalized.
};
