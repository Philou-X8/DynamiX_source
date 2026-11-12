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
#include "drive_comms.h"
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
constexpr int8_t FORCE_SPI_MISO  = 19;   // DOU
constexpr int8_t FORCE_SPI_MOSI  = 23;   // DIN
constexpr int8_t FORCE_ADC_CS    = 25;
constexpr int8_t FORCE_ADC_DRDY  = 5;    // needs an internal pull-up, so not an input-only pin (34-39) // swapped from 4 to 5
constexpr int8_t FORCE_ADC_RESET = 26;
constexpr int8_t FORCE_ADC_SYNC  = 27;

constexpr int8_t POS_SPI_SCK     = 14;   // HSPI default pins
constexpr int8_t POS_SPI_MISO    = 34; // was 12   // WARNING: GPIO12 is a boot strapping pin (flash voltage).
                                         // If the ADC's DOUT is high at power-up the ESP32 can fail
                                         // to boot. If so, move it to any other free GPIO (the SPI
                                         // bus can use any pin through the GPIO matrix).
constexpr int8_t POS_SPI_MOSI    = 13;
constexpr int8_t POS_ADC_CS      = 33;
constexpr int8_t POS_ADC_DRDY    = 32;
constexpr int8_t POS_ADC_RESET   = 21;
constexpr int8_t POS_ADC_SYNC    = 22;

// Motor drive, RS-485 on UART2.
// WARNING: the tested motor_drive.ino used GPIO4 for DE/RE, but GPIO4 is the
// force ADC's DRDY above. Both can't share it. GPIO5 is the free pin used
// here (it is a strapping pin, but fine as an output). Either rewire DE/RE to
// GPIO5, or move FORCE_ADC_DRDY elsewhere and set this back to 4.
constexpr int8_t DRIVE_RX_PIN    = 16; // RO
constexpr int8_t DRIVE_TX_PIN    = 17; // DI
constexpr int8_t DRIVE_DE_RE_PIN = 4;  // DE/RE // swapped from 5 to 4

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
DriveComms g_driveComms;

uint32_t loop_period = 2000; // 2000us -> 2ms -> 500Hz
uint32_t loop_cutoff = 0;

// Rolling buffers of the last SENSOR_AVG_SIZE readings (written by the main
// loop only, so no lock needed).
constexpr size_t SENSOR_AVG_SIZE = 10;
int32_t sensor_pos_avg[SENSOR_AVG_SIZE] = {0};
size_t sensor_pos_avg_i = 0;
int32_t sensor_force_avg[SENSOR_AVG_SIZE] = {0};
size_t sensor_force_avg_i = 0;
size_t sensor_avg_count = 0; // readings stored so far, saturates at SENSOR_AVG_SIZE

// Range of the position average seen so far (signed 24-bit). Starts inverted
// (min above max) so the first full window sets both.
int32_t sensor_pos_max = -8388608;
int32_t sensor_pos_min = 8388607;

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
    g_positionTaskParams.adc.pga = PGA_1;
    g_positionTaskParams.adc.drate = DRATE_1000SPS;
    g_positionTaskParams.adc.mux = SING_0;
    g_positionTaskParams.adc.buffer = false;

    g_motorControl.begin();
    g_stateMachine.begin(&g_motorControl, &g_dataBuffer, &g_commsTask, &g_driveComms);
    g_commsTask.begin(&g_dataBuffer, &g_stateMachine, SERIAL_BAUD);
    g_driveComms.begin(DRIVE_RX_PIN, DRIVE_TX_PIN, DRIVE_DE_RE_PIN);

    xTaskCreatePinnedToCore(sensorTaskFn, "sensor_position", SENSOR_TASK_STACK_SIZE,
                             &g_positionTaskParams, 2, nullptr, SENSOR_TASK_CORE);
    xTaskCreatePinnedToCore(sensorTaskFn, "sensor_force", SENSOR_TASK_STACK_SIZE,
                             &g_forceTaskParams, 2, nullptr, SENSOR_TASK_CORE);

    xTaskCreatePinnedToCore(CommsTask::sendTaskFn, "comms_send", 4096,
                             &g_commsTask, 2, nullptr, COMMS_TASK_CORE);
    xTaskCreatePinnedToCore(CommsTask::recvTaskFn, "comms_recv", 4096,
                             &g_commsTask, 2, nullptr, COMMS_TASK_CORE);
    xTaskCreatePinnedToCore(DriveComms::taskFn, "drive_comms", 4096,
                             &g_driveComms, 2, nullptr, COMMS_TASK_CORE);

    // Main loop runs as the Arduino loop() itself, implicitly on the
    // "loop task" -- pin it explicitly if loop()'s default core placement
    // ends up colliding with the above (commonly loopTask runs on core 1
    // by default on ESP32 Arduino, which is why sensor/comms tasks are
    // pinned to core 0 above; confirm on your specific board setup).

    loop_cutoff = micros() + loop_period;
}

// --- Rolling averages / calibration ------------------------------------------

// Adds the newest readings to the rolling buffers, then updates the recorded
// position range from the position average. The range is only updated once
// the buffer is full, so the zeros it starts with can't drag the minimum down.
void updateRollingAverages(int32_t posRaw, int32_t forceRaw) {
    sensor_pos_avg[sensor_pos_avg_i] = posRaw;
    sensor_pos_avg_i = (sensor_pos_avg_i + 1) % SENSOR_AVG_SIZE;
    sensor_force_avg[sensor_force_avg_i] = forceRaw;
    sensor_force_avg_i = (sensor_force_avg_i + 1) % SENSOR_AVG_SIZE;
    if (sensor_avg_count < SENSOR_AVG_SIZE) {
        sensor_avg_count++;
        if (sensor_avg_count < SENSOR_AVG_SIZE) {
            return;
        }
    }

    int64_t sum = 0;
    for (size_t i = 0; i < SENSOR_AVG_SIZE; i++) {
        sum += sensor_pos_avg[i];
    }
    int32_t posAvg = (int32_t)(sum / (int64_t)SENSOR_AVG_SIZE);

    if (posAvg < sensor_pos_min) sensor_pos_min = posAvg;
    if (posAvg > sensor_pos_max) sensor_pos_max = posAvg;
}

// TEMP: answers GUI_calibrate with ESP_calibraion_data. Payload is 9 bytes,
// each value encoded like a data packet field (24 bit signed, big-endian):
// [pos min][pos max][average force].
void handleCalibrateRequest() {
    int32_t posMin = sensor_pos_min;
    int32_t posMax = sensor_pos_max;
    if (posMin > posMax) {
        posMin = 0; // no full window yet, so no range recorded
        posMax = 0;
    }

    int32_t forceAvg = 0;
    if (sensor_avg_count > 0) {
        int64_t sum = 0;
        for (size_t i = 0; i < sensor_avg_count; i++) {
            sum += sensor_force_avg[i]; // the first sensor_avg_count slots are the valid ones
        }
        forceAvg = (int32_t)(sum / (int64_t)sensor_avg_count);
    }

    uint8_t payload[9];
    writeInt24BE(posMin, payload);
    writeInt24BE(posMax, payload + 3);
    writeInt24BE(forceAvg, payload + 6);
    g_commsTask.enqueuePriorityMessage(Cmd::ESP_calibration_data, payload, sizeof(payload));
}

// TEMP: GUI_set_rpm carries a complete Modbus frame for the drive. Forward it.
void handleSetRpm(const Instruction& instr) {
    if (!g_driveComms.sendFrame(instr.payload, instr.payloadSize)) {
        const char msg[] = "Bad drive frame";
        g_commsTask.enqueuePriorityMessage(Cmd::ESP_error, (const uint8_t*)msg, sizeof(msg) - 1);
    }
}

// --- Main loop -----------------------------------------------------------

void loop() {
    //uint32_t loopStartMs = millis();
    uint32_t loop_time = micros();

    // Drain whatever the drive sent back. Nothing uses it yet, but the queue
    // would fill up and drop new frames if it were never emptied.
    DriveFrame driveFrame;
    while (g_driveComms.popResponse(driveFrame)) {
        // TODO: parse the drive's reply (fault codes, echoed writes, ...)
    }

    // handle incomming serial instructions
    Instruction instr;
    if (g_commsTask.popInstruction(instr)) {
        g_stateMachine.handleInstruction(instr);

        // Debug commands that don't go through the state machine
        if (instr.flag == Cmd::GUI_calibrate) {
            handleCalibrateRequest();
        } else if (instr.flag == Cmd::GUI_set_rpm) {
            handleSetRpm(instr);
        }
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

        int32_t posRaw, forceRaw;
        uint32_t posSeq, forceSeq;
        sharedSensorDataRead(g_positionSensor, posRaw, posSeq);
        sharedSensorDataRead(g_forceSensor, forceRaw, forceSeq);

        updateRollingAverages(posRaw, forceRaw);

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
