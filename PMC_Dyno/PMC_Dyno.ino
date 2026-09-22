// PMC_Dyno.ino
//
// Top-level sketch: creates the sensor tasks and comms tasks, then runs
// the main state-machine loop. See PMC_semaine3 design doc for the full
// architecture (threading model, framing, state machine).
//
// This is a first-draft skeleton: sensor reads, RS-485 motor control, and
// instruction payload decoding are all stubbed with TODOs (see the
// respective .cpp files). What IS wired up end-to-end here is the
// threading/data-flow skeleton itself: sensor tasks -> shared structs ->
// main loop -> double buffer -> comms send task -> UART, and
// UART -> comms recv task -> instruction queue -> main loop -> state
// machine.

#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

#include "data_buffer.h"
#include "protocol.h"
#include "sensor_task.h"
#include "motor_control.h"
#include "state_machine.h"
#include "comms_task.h"

// --- Configuration ---------------------------------------------------

constexpr uint32_t SERIAL_BAUD = 115200; // design doc section 5

// TODO: fill in real GPIO assignments for each ADS1256 (CS/DRDY/RESET) and
// confirm whether both ADCs share one hardware SPI bus or use two
// independent ones (VSPI/HSPI) -- see earlier discussion on the library's
// SPIClass support before wiring this up physically.
constexpr int SENSOR_TASK_STACK_SIZE = 4096;
constexpr int MAIN_LOOP_CORE = 1;
constexpr int SENSOR_TASK_CORE = 0;
constexpr int COMMS_TASK_CORE = 0;

// --- Globals -----------------------------------------------------------

SharedSensorData g_positionSensor; // linear potentiometer
SharedSensorData g_forceSensor;    // load cell

SensorTaskParams g_positionTaskParams;
SensorTaskParams g_forceTaskParams;

DoubleBuffer g_dataBuffer;
MotorControl g_motorControl;
StateMachine g_stateMachine;
CommsTask g_commsTask;

// --- Setup ---------------------------------------------------------------

void setup() {
    sharedSensorDataInit(g_positionSensor);
    sharedSensorDataInit(g_forceSensor);

    g_positionTaskParams.target = &g_positionSensor;
    g_forceTaskParams.target = &g_forceSensor;

    g_motorControl.begin();
    g_stateMachine.begin(&g_motorControl, &g_dataBuffer);
    g_commsTask.begin(&g_dataBuffer, &g_stateMachine, SERIAL_BAUD);

    xTaskCreatePinnedToCore(sensorTaskFn, "sensor_position", SENSOR_TASK_STACK_SIZE,
                             &g_positionTaskParams, 2, nullptr, SENSOR_TASK_CORE);
    xTaskCreatePinnedToCore(sensorTaskFn, "sensor_force", SENSOR_TASK_STACK_SIZE,
                             &g_forceTaskParams, 2, nullptr, SENSOR_TASK_CORE);

    xTaskCreatePinnedToCore(CommsTask::sendTaskFn, "comms_send", 4096,
                             &g_commsTask, 2, nullptr, COMMS_TASK_CORE);
    xTaskCreatePinnedToCore(CommsTask::recvTaskFn, "comms_recv", 4096,
                             &g_commsTask, 2, nullptr, COMMS_TASK_CORE);

    // Main loop runs as the Arduino loop() itself, implicitly on the
    // "loop task" -- pin it explicitly if loop()'s default core placement
    // ends up colliding with the above (commonly loopTask runs on core 1
    // by default on ESP32 Arduino, which is why sensor/comms tasks are
    // pinned to core 0 above; confirm on your specific board setup).
}

// --- Main loop -----------------------------------------------------------

void loop() {
    uint32_t loopStartMs = millis();

    int32_t posRaw, forceRaw;
    uint32_t posSeq, forceSeq;
    sharedSensorDataRead(g_positionSensor, posRaw, posSeq);
    sharedSensorDataRead(g_forceSensor, forceRaw, forceSeq);

    // TODO: track posSeq/forceSeq across iterations; if either hasn't
    // incremented in N iterations, call
    // g_stateMachine.raiseFault(FaultReason::SensorStalled) -- design doc
    // section 7.

    Instruction instr;
    if (g_commsTask.popInstruction(instr)) {
        g_stateMachine.handleInstruction(instr);
    }

    g_stateMachine.update(posRaw, forceRaw, loopStartMs);

    // TODO: measure actual loop iteration time here and compare against
    // the ~2ms budget; call raiseFault(FaultReason::LoopTimingDrift) if
    // exceeded (design doc section 7).

    // No fixed delay here deliberately -- the loop is meant to run as
    // close to 500 Hz as the work above allows. Add pacing/backoff only if
    // profiling shows it's needed.
}
