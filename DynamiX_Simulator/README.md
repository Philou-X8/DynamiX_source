# DynamiX_Simulator

The shock dyno without the machine: the ESP32 firmware's logic running on the PC, with a simulated drive, scotch yoke,
shock, load cell, position sensor and IR thermometer. The HMI talks to it through `DynamiX_Simulator.dll` instead of a
COM port: choose **Simulator** (first entry of the COM port list) and press Connect.

## Build and use

1. Run `build.bat` (MSVC x64; set `VCVARS` if your `vcvars64.bat` is not in the default place). It writes
   `bin\DynamiX_Simulator.dll` and `bin\selftest.exe`.
2. Build the HMI. Its project copies the DLL next to the exe when it exists. Without it, the HMI works as before and
   selecting Simulator says the DLL is missing.

`bin\selftest.exe` talks to the DLL like the HMI does (handshake, TAR, calibration, a two-speed run, reboot) at 4x speed
and prints `SIM OK`.

## How it is made

- `sim.cpp` compiles the firmware's own `state_machine.cpp`, `motor_drive.cpp`, `drive_protocol.cpp` and `protocol.cpp`
  from `..\DynamiX_Firmware`, **unmodified**. Nothing is copied: a firmware change is in the simulator after a rebuild.
- What touches the hardware is replaced in `sim.cpp`: `GuiLink` (byte queues), `DriveLink` (the simulated drive answers
  the Modbus frames), `PointBuffer` (same ring, no FreeRTOS lock), and the sensors and safety relay (the `Plant`).
  `tick()` repeats `loop()` of `DynamiX_Firmware.ino` every 2 ms, in real time.
- C interface (cdecl): `dynamix_sim_open/close/write/read/available`, `dynamix_sim_set_shock_mounted`,
  `dynamix_sim_set_speed` (tests only: the HMI heartbeat runs on the real clock).
- HMI side: `DynamiX_HMI\Serial\DynoPort.cs`, a `SerialPort` look-alike that does what `SerialPort` does except for the
  name "Simulator".

## The machine

Numbers are at the top of the `Plant` section of `sim.cpp`: 25 mm stroke, 2 N·s/mm compression and 3.5 rebound damping
(plus a quadratic part), 100 N preload and 4 N/mm gas spring, a 2 N sawtooth locked to the position, noise, and the
damper heating with the damping power. The sensor scales equal the HMI's default calibration. The shock is taken off
during a TAR (the empty load is the 13.4 N fixture) and put back after it.

Not simulated: drive faults and the RS-485 timeout, the e-stop and door (the safety relay is always closed), ADC faults.
