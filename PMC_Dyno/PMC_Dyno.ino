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
#include "command_flags.h"
#include "ADS1256.h" // only for the PGA_x / DRATE_x / DIFF_x_y config macros below.
                     // It also #defines RESET, SYNC, WAKEUP, ... so don't use those names.

// --- Configuration ---------------------------------------------------

constexpr uint32_t SERIAL_BAUD = 115200; // design doc section 5

// Pins reserved elsewhere: UART0 (GPIO1/3, USB link to the GUI) and UART2
// (GPIO16/17, RS-485 drive). Everything below avoids those, the flash pins
// (6-11) and the strapping pins (0, 2, 5, 12*, 15). *HSPI MISO is the exception, see below.
//
// Each ADS1256 has its own SPI bus: force on VSPI, position on HSPI.
constexpr int8_t FORCE_SPI_SCK   = 18;   // VSPI default pins
constexpr int8_t FORCE_SPI_MISO  = 19;
constexpr int8_t FORCE_SPI_MOSI  = 23;
constexpr int8_t FORCE_ADC_CS    = 25;
constexpr int8_t FORCE_ADC_DRDY  = 4;    // needs an internal pull-up, so not an input-only pin (34-39)
constexpr int8_t FORCE_ADC_RESET = 26;
constexpr int8_t FORCE_ADC_SYNC  = 27;

constexpr int8_t POS_SPI_SCK     = 14;   // HSPI default pins
constexpr int8_t POS_SPI_MISO    = 12;   // WARNING: GPIO12 is a boot strapping pin (flash voltage).
                                         // If the ADC's DOUT is high at power-up the ESP32 can fail
                                         // to boot. If so, move it to any other free GPIO (the SPI
                                         // bus can use any pin through the GPIO matrix).
constexpr int8_t POS_SPI_MOSI    = 13;
constexpr int8_t POS_ADC_CS      = 33;
constexpr int8_t POS_ADC_DRDY    = 32;
constexpr int8_t POS_ADC_RESET   = 21;
constexpr int8_t POS_ADC_SYNC    = 22;

constexpr float ADC_VREF = 2.500f;       // measure the real reference on the board

SPIClass g_hspi(HSPI);                   // second SPI bus, for the position ADC (the first is the global SPI)

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

uint32_t loop_period = 2000; // 2000us -> 2ms -> 500Hz
uint32_t loop_cutoff = 0;


sensor_pos_avg[10] = {0}; // rolling buffer of the last 10 values
sensor_pos_avg_i = 0;
sensor_force_avg[10] = {0}; // rolling buffer of the last 10 values
sensor_force_avg_i = 0;

sensor_pos_max;
sensor_pos_min;

// --- Setup ---------------------------------------------------------------

void setup() {
    sharedSensorDataInit(g_positionSensor);
    sharedSensorDataInit(g_forceSensor);

    // Load cell on AIN1/AIN2 of the board (chip channels 0/1): bridge on
    // 5 V excitation, so gain 64 (+-39 mV) covers the full range.
    g_forceTaskParams.target = &g_forceSensor;
    g_forceTaskParams.spi = &SPI;
    g_forceTaskParams.sckPin = FORCE_SPI_SCK;
    g_forceTaskParams.misoPin = FORCE_SPI_MISO;
    g_forceTaskParams.mosiPin = FORCE_SPI_MOSI;
    g_forceTaskParams.adc.drdyPin = FORCE_ADC_DRDY;
    g_forceTaskParams.adc.resetPin = FORCE_ADC_RESET;
    g_forceTaskParams.adc.syncPin = FORCE_ADC_SYNC;
    g_forceTaskParams.adc.csPin = FORCE_ADC_CS;
    g_forceTaskParams.adc.vref = ADC_VREF;
    g_forceTaskParams.adc.pga = PGA_64;
    g_forceTaskParams.adc.drate = DRATE_1000SPS;
    g_forceTaskParams.adc.mux = DIFF_0_1;
    g_forceTaskParams.adc.buffer = true;

    // Potentiometer: placeholder, a copy of the load cell settings on its own
    // pins. TODO: real settings (probably PGA_1, a single-ended input) once it
    // is wired. With nothing attached the task detects that and publishes a
    // blank value instead of hanging.
    g_positionTaskParams.target = &g_positionSensor;
    g_positionTaskParams.spi = &g_hspi;
    g_positionTaskParams.sckPin = POS_SPI_SCK;
    g_positionTaskParams.misoPin = POS_SPI_MISO;
    g_positionTaskParams.mosiPin = POS_SPI_MOSI;
    g_positionTaskParams.adc.drdyPin = POS_ADC_DRDY;
    g_positionTaskParams.adc.resetPin = POS_ADC_RESET;
    g_positionTaskParams.adc.syncPin = POS_ADC_SYNC;
    g_positionTaskParams.adc.csPin = POS_ADC_CS;
    g_positionTaskParams.adc.vref = ADC_VREF;
    g_positionTaskParams.adc.pga = PGA_64;
    g_positionTaskParams.adc.drate = DRATE_1000SPS;
    g_positionTaskParams.adc.mux = DIFF_0_1;
    g_positionTaskParams.adc.buffer = true;

    g_motorControl.begin();
    g_stateMachine.begin(&g_motorControl, &g_dataBuffer, &g_commsTask);
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

    loop_cutoff = micros() + loop_period;
}

// --- Main loop -----------------------------------------------------------

void loop() {
    //uint32_t loopStartMs = millis();
    uint32_t loop_time = micros();

    // handle incomming serial instructions
    Instruction instr;
    if (g_commsTask.popInstruction(instr)) {
        g_stateMachine.handleInstruction(instr);
    }

    if (loop_time >= loop_cutoff){
        loop_cutoff += loop_period;

        if (loop_time >= loop_cutoff){
            // loop_time is still greater than loop_cutoff
            // this mean we missed an entire loop
            // we are NOT ABLE to run at the set frequency

            // We'll need to enter some form of panic state 
            String myString = "LOOP MISSED";
            uint8_t buf[12];
            myString.getBytes(buf, sizeof(buf));
            g_commsTask.enqueuePriorityMessage(Cmd::ESP_message, buf, sizeof(buf));
        }

        uint32_t posRaw, forceRaw;
        uint32_t posSeq, forceSeq;
        sharedSensorDataRead(g_positionSensor, posRaw, posSeq);
        sharedSensorDataRead(g_forceSensor, forceRaw, forceSeq);

        // TODO: add the new values to the rolling buffer and return the average.
        // Also update the min and max position if the average value is outside the current recorded range. 

        // TODO: track posSeq/forceSeq across iterations; if either hasn't
        // incremented in N iterations, call
        // g_stateMachine.raiseFault(FaultReason::SensorStalled) -- design doc
        // section 7.

        g_stateMachine.update(posRaw, forceRaw, loop_time);

        // TODO: measure actual loop iteration time here and compare against
        // the ~2ms budget; call raiseFault(FaultReason::LoopTimingDrift) if
        // exceeded (design doc section 7).

        // No fixed delay here deliberately -- the loop is meant to run as
        // close to 500 Hz as the work above allows. Add pacing/backoff only if
        // profiling shows it's needed.
    }

    
}
