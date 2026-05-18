@echo off
chcp 65001 >nul
title Nexus Control - Build
cls

echo.
echo  Nexus Control - Build Tool
echo  ==========================
echo.

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK not found.
    echo Install from: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo .NET SDK: v%DOTNET_VER%
echo.

set PROJ_DIR=%~dp0
set PROJ_DIR=%PROJ_DIR:~0,-1%

echo Checking for running process...
tasklist /fi "imagename eq NexusControl.exe" 2>nul | find /i "NexusControl.exe" >nul
if %errorlevel% equ 0 (
    echo Stopping NexusControl.exe...
    taskkill /f /im NexusControl.exe >nul 2>&1
    timeout /t 1 /nobreak >nul
    echo Process stopped.
) else (
    echo No running instance found.
)
echo.

echo Generating icon...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PROJ_DIR%\generate_icon.ps1" "%PROJ_DIR%"
if %errorlevel% neq 0 (
    echo [ERROR] PowerShell returned an error while generating the icon.
    pause
    exit /b 1
)
if not exist "%PROJ_DIR%\nexus.ico" (
    echo [ERROR] nexus.ico was not created.
    pause
    exit /b 1
)
echo Icon OK.
echo.

set OUT=%PROJ_DIR%\dist

echo Cleaning previous build...
if exist "%OUT%" rmdir /s /q "%OUT%"
echo.

echo Compiling...
echo.

dotnet publish "%PROJ_DIR%\NexusControl.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained ^
    -o "%OUT%" ^
    /p:DebugType=none ^
    /p:DebugSymbols=false ^
    /nowarn:WFAC010

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed.
    echo.
    pause
    exit /b 1
)

del /q "%OUT%\*.pdb" 2>nul

echo.
echo  ==========================
echo   Build complete!
echo  ==========================
echo.
echo Output: %OUT%\NexusControl.exe
echo.
explorer "%OUT%"
pause
