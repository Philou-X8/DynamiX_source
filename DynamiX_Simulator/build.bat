@echo off
rem Builds DynamiX_Simulator.dll with MSVC (x64) into bin\. The firmware's logic is compiled from
rem ..\DynamiX_Firmware unmodified; sim.cpp is the simulated hardware and machine.
rem Set VCVARS to your Visual Studio's vcvars64.bat if it isn't here.
setlocal
if "%VCVARS%"=="" set VCVARS=F:\Programming\IDE\VisualStudio\2022\Community\VC\Auxiliary\Build\vcvars64.bat
call "%VCVARS%" >nul 2>&1 || (echo vcvars64.bat not found: set VCVARS & exit /b 1)

set FW=%~dp0..\DynamiX_Firmware
if not exist "%~dp0bin" mkdir "%~dp0bin"
if not exist "%~dp0obj" mkdir "%~dp0obj"

cl /nologo /std:c++17 /O2 /EHsc /W3 /LD /Fo"%~dp0obj\\" /Fe"%~dp0bin\DynamiX_Simulator.dll" ^
   "%~dp0sim.cpp" "%FW%\state_machine.cpp" "%FW%\motor_drive.cpp" "%FW%\drive_protocol.cpp" "%FW%\protocol.cpp" ^
   || exit /b 1

rem the self test: talks to the DLL like the HMI does
cl /nologo /std:c++17 /O2 /EHsc /W3 /Fo"%~dp0obj\\" /Fe"%~dp0bin\selftest.exe" "%~dp0selftest.cpp" || exit /b 1
echo built %~dp0bin\DynamiX_Simulator.dll
