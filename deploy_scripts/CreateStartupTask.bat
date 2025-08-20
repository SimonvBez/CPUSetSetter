@echo off
setlocal

:: Restart with Admin rights if not already
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell start -verb runas '%0'
    exit /b
)

:: Get the folder where this script is located
set "scriptDir=%~dp0"

:: Set the exe path to CPUSetSetter.exe in the same folder
set "exePath=%scriptDir%CPUSetSetter.exe"

if not exist "%exePath%" (
    echo File not found: %exePath%
    pause
    exit /b
)

for /f %%I in ('whoami') do set fullUser=%%I

:: Define task name
set taskName=CPUSetSetter

:: Delete existing task if it exists
schtasks /Delete /TN "%taskName%" /F >nul 2>&1

:: Create the task
schtasks /Create /TN "%taskName%" /TR "%exePath%" /SC ONLOGON /RL HIGHEST /F /RU "%userName%"

pause
endlocal
