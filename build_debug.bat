@echo off
setlocal enabledelayedexpansion

echo.
echo ========================================
echo.
echo GRC2 Modding Build Script
echo.
echo ========================================
echo.

:: Project settings
set "PROJECT_NAME=GRC2"
set "PROJECT_DIR=GRC2"
set "SOLUTION_FILE=GRC2.sln"
set "GAME_PATH=H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle"
set "SOURCE_ROOT=H:\source\repos\GRC2"

:: Build paths
set "DLL_NAME=%PROJECT_NAME%.dll"
set "MODS_DIR=%GAME_PATH%\Mods"
set "SOURCE_DLL=%SOURCE_ROOT%\%PROJECT_DIR%\bin\Debug\net472\%DLL_NAME%"
set "TARGET_DLL=%MODS_DIR%\%DLL_NAME%"

:: Find MSBuild
set "MSBUILD_PATH="

if exist "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)
if "!MSBUILD_PATH!"=="" if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
)

if "!MSBUILD_PATH!"=="" (
    echo [ERROR] MSBuild not found.
    echo [ERROR] Please check if Visual Studio is installed.
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

echo [INFO] MSBuild path: !MSBUILD_PATH!
echo.

:: Get script directory
set "SCRIPT_DIR=%~dp0"
set "SOLUTION_PATH=!SCRIPT_DIR!!SOLUTION_FILE!"

echo [INFO] Starting Debug build...
echo [INFO] GamePath: !GAME_PATH!
echo.

:: Restore NuGet packages
echo [INFO] Restoring NuGet packages...
"!MSBUILD_PATH!" "!SOLUTION_PATH!" /p:Configuration=Debug /p:Platform="Any CPU" /p:GamePath="!GAME_PATH!" /t:Restore /v:minimal /nologo

:: Build project
echo [INFO] Building project...
"!MSBUILD_PATH!" "!SOLUTION_PATH!" /p:Configuration=Debug /p:Platform="Any CPU" /p:GamePath="!GAME_PATH!" /t:Build /v:minimal /nologo

if errorlevel 1 (
    echo.
    echo ========================================
    echo [ERROR] Build failed
    echo ========================================
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

echo.
echo ========================================
echo [SUCCESS] Build completed
echo ========================================
echo.

:: Verify DLL file
if not exist "!SOURCE_DLL!" (
    echo [ERROR] DLL file not found: !SOURCE_DLL!
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

for %%F in ("!SOURCE_DLL!") do (
    set "FILE_SIZE=%%~zF"
    set "FILE_TIME=%%~tF"
)

echo [INFO] Built DLL file: !SOURCE_DLL!
echo [INFO] File size: !FILE_SIZE! bytes
echo [INFO] Modified time: !FILE_TIME!
echo.

if !FILE_SIZE! LSS 1024 (
    echo [ERROR] DLL file size is too small: !FILE_SIZE! bytes
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

:: Copy to Mods directory
echo ========================================
echo [STEP] Copying DLL to Mods directory...
echo ========================================
echo.

if not exist "!GAME_PATH!" (
    echo [ERROR] Game directory not found: !GAME_PATH!
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

if not exist "!MODS_DIR!" (
    echo [INFO] Creating Mods directory...
    mkdir "!MODS_DIR!"
)

echo [INFO] Copying !SOURCE_DLL!
echo [INFO]      to !TARGET_DLL!
echo.

copy /Y "!SOURCE_DLL!" "!TARGET_DLL!" >nul

if errorlevel 1 (
    echo [ERROR] File copy failed
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

:: Verify copied file
for %%F in ("!TARGET_DLL!") do set "COPIED_SIZE=%%~zF"

if not "!FILE_SIZE!"=="!COPIED_SIZE!" (
    echo [ERROR] File sizes do not match!
    echo [ERROR] Source: !FILE_SIZE! bytes
    echo [ERROR] Copied: !COPIED_SIZE! bytes
    echo [INFO] Closing automatically in 60 seconds...
    call :Wait60Seconds
    exit /b 1
)

echo ========================================
echo [SUCCESS] DLL copied successfully
echo ========================================
echo.
echo [INFO]  Source: !SOURCE_DLL!
echo [INFO]  Target: !TARGET_DLL!
echo [INFO]  File size: !COPIED_SIZE! bytes
echo.

echo [INFO] Closing automatically in 60 seconds...
call :Wait60Seconds
exit /b 0

:Wait60Seconds
where powershell >nul 2>&1
if not errorlevel 1 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Sleep -Seconds 60" >nul 2>&1
    goto :eof
)

ping 127.0.0.1 -n 61 >nul
goto :eof
