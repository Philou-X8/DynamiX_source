@echo off
rem Builds and runs the host tests with MSVC: the firmware's logic (state
rem machine, Modbus frames, filters, cycle counting) against a simulated
rem machine. The Arduino IDE ignores this folder.
rem
rem Set VCVARS to your Visual Studio's vcvars64.bat if it isn't here.
setlocal
if "%VCVARS%"=="" set VCVARS=F:\Programming\IDE\VisualStudio\2022\Community\VC\Auxiliary\Build\vcvars64.bat
call "%VCVARS%" >nul 2>&1 || exit /b 1

set SRC=%~dp0..
set OUT=%TEMP%\pmc_dyno_host_tests
if not exist "%OUT%" mkdir "%OUT%"

cl /nologo /std:c++17 /W4 /EHsc /O1 /Fo"%OUT%\\" /Fe"%OUT%\tests.exe" ^
   "%~dp0tests.cpp" "%~dp0fakes.cpp" ^
   "%SRC%\state_machine.cpp" "%SRC%\motor_drive.cpp" "%SRC%\drive_protocol.cpp" "%SRC%\protocol.cpp" ^
   || exit /b 1

"%OUT%\tests.exe"
