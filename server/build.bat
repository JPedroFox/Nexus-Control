@echo off
:: ============================================================
::  build.bat — Compila o Nexus Control em um único .exe
::  Basta dar dois cliques. Não precisa de nada instalado além
::  do .NET SDK (https://dot.net).
:: ============================================================

title Compilando Nexus Control...
cls

echo.
echo  ╔══════════════════════════════════════╗
echo  ║       Nexus Control — Build Tool     ║
echo  ╚══════════════════════════════════════╝
echo.

:: ── Verifica se o .NET SDK está instalado ───────────────────
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERRO] .NET SDK nao encontrado.
    echo.
    echo  Instale em: https://dotnet.microsoft.com/download
    echo  Baixe a versao ".NET 8 SDK" para Windows x64.
    echo.
    pause
    exit /b 1
)

for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo  .NET SDK encontrado: v%DOTNET_VER%
echo.

:: ── Pasta de saída ──────────────────────────────────────────
set OUT=dist

echo  Limpando build anterior...
if exist "%OUT%" rmdir /s /q "%OUT%"

echo  Compilando e empacotando (pode levar ~30 segundos)...
echo.

dotnet publish -c Release ^
               -r win-x64 ^
               --self-contained ^
               -o "%OUT%" ^
               /p:DebugType=none ^
               /p:DebugSymbols=false

if %errorlevel% neq 0 (
    echo.
    echo  [ERRO] Build falhou. Veja as mensagens acima.
    echo.
    pause
    exit /b 1
)

del /q "%OUT%\*.pdb" 2>nul

echo.
echo  ╔══════════════════════════════════════╗
echo  ║           Build concluido!           ║
echo  ╚══════════════════════════════════════╝
echo.
echo  Arquivo gerado:
echo    %~dp0%OUT%\NexusControl.exe
echo.
echo  Abrindo a pasta...
explorer "%~dp0%OUT%"

pause
