@echo off
REM ============================================================
REM  Build PKHaX (PKHeX fork) -> PKHaX.exe
REM  Double-click this file. Needs the .NET 10 SDK installed.
REM  Output runs on Windows and needs the .NET 10 Desktop Runtime.
REM ============================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [X] .NET SDK not found on PATH.
  echo     Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
  echo     ^(the SDK, not just the runtime^), then run this again.
  pause
  exit /b 1
)

echo === Building PKHaX ^(Release, win-x64^). This takes a minute or two... ===
dotnet publish PKHeX.WinForms -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
if errorlevel 1 (
  echo [X] Build failed - read the dotnet output above.
  pause
  exit /b 1
)

set "OUTDIR=PKHeX.WinForms\bin\Release\net10.0-windows\win-x64\publish"
if not exist "%OUTDIR%\PKHeX.exe" (
  echo [X] Build finished but PKHeX.exe was not found in:
  echo     %OUTDIR%
  pause
  exit /b 1
)

copy /y "%OUTDIR%\PKHeX.exe" "%OUTDIR%\PKHaX.exe" >nul
echo.
echo [OK] PKHaX built:
echo      %CD%\%OUTDIR%\PKHaX.exe
echo.
echo Run PKHaX.exe ^(the "HaX" name turns on illegal-edit mode^).
echo It needs the .NET 10 Desktop Runtime. Keep any files next to it.
pause
