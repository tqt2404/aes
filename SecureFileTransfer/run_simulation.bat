@echo off
REM Secure File Transfer - Dual Instance Simulation
REM Mô phỏng 2 máy tính: Receiver + Sender trên cùng 1 PC

setlocal enabledelayedexpansion

set CONFIG=Debug
set APP_PATH=.\src\bin\%CONFIG%\net8.0-windows\SecureFileTransfer.exe

if not exist "%APP_PATH%" (
    echo.
    echo [ERROR] App not found: %APP_PATH%
    echo [INFO] Building project...
    call dotnet build --configuration %CONFIG%
)

cls
echo.
echo ================================================================================
echo          SECURE FILE TRANSFER - Dual Instance Simulation
echo ================================================================================
echo.
echo [CONFIG]
echo   Build Type:      %CONFIG%
echo   Receiver Port:   9000
echo   Sender connects: 127.0.0.1:9000
echo.
echo ================================================================================
echo          INSTRUCTIONS
echo ================================================================================
echo.
echo WINDOW 1 (Receiver - Machine B):
echo   1. Select "Receiver View"
echo   2. Port: 9000 (already set)
echo   3. Click "Start Listening"
echo.
echo WINDOW 2 (Sender - Machine A):
echo   1. Select "Sender View"
echo   2. IP Address: 127.0.0.1
echo   3. Port: 9000
echo   4. Select file to send
echo   5. Enter password
echo   6. Click "Send"
echo.
echo ================================================================================
echo.

echo [INFO] Starting RECEIVER (Machine B)...
start SecureFileTransfer-Receiver "%APP_PATH%"

echo [INFO] Waiting 2 seconds...
timeout /t 2 /nobreak

echo [INFO] Starting SENDER (Machine A)...
start SecureFileTransfer-Sender "%APP_PATH%"

echo.
echo [SUCCESS] Both instances started!
echo [INFO] Check taskbar for 2 SecureFileTransfer windows
echo.
pause
