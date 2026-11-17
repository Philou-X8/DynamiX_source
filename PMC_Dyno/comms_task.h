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

    // Enqueue a command (ack/error/heartbeat) for the send task to
    // prioritize ahead of the next data chunk. 'payload' may be null when
    // payloadSize is 0. Returns false if the frame can't be built or the
    // priority queue is full.
    bool enqueuePriorityMessage(uint8_t flag, const uint8_t* payload, size_t payloadSize);

    // Called from the main loop (not the recv task) so all state
    // transitions stay owned by a single thread. Returns true and fills
    // outInstr if one was pending; non-blocking.
    bool popInstruction(Instruction& outInstr);

    // Asks the send task to also send the points sitting in a partly filled
    // buffer (normally only a full buffer is sent). Called at the end of a
    // run so its last points reach the GUI. Safe to call from the main loop.
    void requestFlush();

    // FreeRTOS task entry points (launched via xTaskCreatePinnedToCore in
    // main.ino).
    static void sendTaskFn(void* pvParameters);
    static void recvTaskFn(void* pvParameters);

private:
    DoubleBuffer* dataBuffer_ = nullptr;
    StateMachine* stateMachine_ = nullptr;

    void* priorityQueueHandle_ = nullptr;   // QueueHandle_t, opaque here
    void* instructionQueueHandle_ = nullptr; // QueueHandle_t, opaque here

    volatile bool flushRequested_ = false;

    // TODO: fixed-size element type for the priority queue (frame bytes +
    // length) and for the instruction queue (Instruction, from
    // state_machine.h) once instruction payload formats are finalized.
};
