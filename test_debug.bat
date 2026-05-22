@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo.
echo GRC2 Test Script
echo.
echo ========================================
echo.

set "SOLUTION_FILE=GRC2.sln"
set "SCRIPT_DIR=%~dp0"
set "SOLUTION_PATH=!SCRIPT_DIR!!SOLUTION_FILE!"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found in PATH.
    echo [INFO] Install .NET SDK and retry.
    echo [INFO] Closing automatically in 60 seconds...
    timeout /t 60 /nobreak >nul
    exit /b 1
)

echo [INFO] Restoring test dependencies...
dotnet restore "!SOLUTION_PATH!" -v minimal
if errorlevel 1 (
    echo [ERROR] dotnet restore failed
    echo [INFO] Closing automatically in 60 seconds...
    timeout /t 60 /nobreak >nul
    exit /b 1
)

echo [INFO] Running unit tests (GRC2.Tests)...
dotnet test "!SOLUTION_PATH!" -c Debug -v minimal
if errorlevel 1 (
    echo.
    echo ========================================
    echo [ERROR] Unit tests failed
    echo ========================================
    echo [INFO] Closing automatically in 60 seconds...
    timeout /t 60 /nobreak >nul
    exit /b 1
)

echo.
echo ========================================
echo [SUCCESS] Unit tests passed
echo ========================================
echo.
echo [INFO] Closing automatically in 60 seconds...
timeout /t 60 /nobreak >nul
exit /b 0
