@echo off
:: ============================================================
::  build.bat — Compila o RemoteServer em um único .exe
::  Basta dar dois cliques. Não precisa de nada instalado além
::  do .NET SDK (https://dot.net).
:: ============================================================

title Compilando RemoteServer...
cls

echo.
echo  ╔══════════════════════════════════════╗
echo  ║       RemoteServer — Build Tool      ║
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

:: ── Exibe versão do SDK encontrada ──────────────────────────
for /f "tokens=*" %%v in ('dotnet --version') do set DOTNET_VER=%%v
echo  .NET SDK encontrado: v%DOTNET_VER%
echo.

:: ── Pasta de saída ──────────────────────────────────────────
set OUT=dist

echo  Limpando build anterior...
if exist "%OUT%" rmdir /s /q "%OUT%"

echo  Compilando e empacotando (pode levar ~30 segundos)...
echo.

:: ── Publish: single-file, self-contained, win-x64 ──────────
::
::   -c Release            → otimizado, sem símbolos de debug
::   -r win-x64            → Windows 64-bit (igual ao .csproj)
::   --self-contained      → embute o runtime .NET no exe
::   --no-restore          → usa cache do NuGet se já restaurou
::   -o "%OUT%"            → pasta de destino
::   /p:DebugType=none     → sem .pdb (arquivo de debug)
::   /p:DebugSymbols=false → sem .pdb
::
dotnet publish -c Release ^
               -r win-x64 ^
               --self-contained ^
               -o "%OUT%" ^
               /p:DebugType=none ^
               /p:DebugSymbols=false

:: ── Verifica se o build funcionou ───────────────────────────
if %errorlevel% neq 0 (
    echo.
    echo  [ERRO] Build falhou. Veja as mensagens acima.
    echo.
    pause
    exit /b 1
)

:: ── Limpa arquivos desnecessários na pasta dist ─────────────
:: O publish single-file já empacota tudo, mas às vezes sobra
:: um .pdb ou arquivo temporário. Remove para deixar limpo.
del /q "%OUT%\*.pdb" 2>nul

:: ── Resultado ───────────────────────────────────────────────
echo.
echo  ╔══════════════════════════════════════╗
echo  ║           Build concluido!           ║
echo  ╚══════════════════════════════════════╝
echo.
echo  Arquivo gerado:
echo    %~dp0%OUT%\RemoteServer.exe
echo.
echo  Abrindo a pasta...
explorer "%~dp0%OUT%"

pause
