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
    echo [ERRO] .NET SDK nao encontrado.
    echo Instale em: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo .NET SDK: v%DOTNET_VER%
echo.

:: Captura o diretorio do bat SEM barra final e SEM aspas
:: %~dp0 termina com \ - o TrimEnd no PS cuida disso, mas passamos sem aspas
:: para evitar o bug de \ escapar a aspa de fechamento no cmd
set PROJ_DIR=%~dp0
set PROJ_DIR=%PROJ_DIR:~0,-1%

echo Gerando icone...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PROJ_DIR%\generate_icon.ps1" "%PROJ_DIR%"
if %errorlevel% neq 0 (
    echo [ERRO] PowerShell retornou erro ao gerar icone.
    pause
    exit /b 1
)
if not exist "%PROJ_DIR%\nexus.ico" (
    echo [ERRO] nexus.ico nao foi criado.
    pause
    exit /b 1
)
echo Icone OK.
echo.

set OUT=%PROJ_DIR%\dist

echo Limpando build anterior...
if exist "%OUT%" rmdir /s /q "%OUT%"
echo.

echo Compilando...
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
    echo [ERRO] Build falhou.
    echo.
    pause
    exit /b 1
)

del /q "%OUT%\*.pdb" 2>nul

echo.
echo  ==========================
echo   Build concluido!
echo  ==========================
echo.
echo Arquivo: %OUT%\NexusControl.exe
echo.
explorer "%OUT%"
pause
