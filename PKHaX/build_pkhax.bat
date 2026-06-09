@echo off
REM ============================================================
REM  Build PKHaX (PKHeX fork) -> PKHaX.exe   [Windows]
REM  Needs the .NET 10 SDK. If it's missing this tries winget.
REM  The built exe needs the .NET 10 Desktop Runtime to RUN.
REM ============================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 goto NEEDSDK
dotnet --list-sdks 2>nul | findstr /b /c:"10." >nul
if not errorlevel 1 goto BUILD

:NEEDSDK
echo .NET 10 SDK not found (you may have an older one - PKHeX needs 10).
where winget >nul 2>nul
if errorlevel 1 goto MANUAL
echo Installing the .NET 10 SDK via winget (may prompt for admin; a few minutes)...
winget install --id Microsoft.DotNet.SDK.10 -e --accept-package-agreements --accept-source-agreements
dotnet --list-sdks 2>nul | findstr /b /c:"10." >nul
if not errorlevel 1 goto BUILD

:MANUAL
echo.
echo [X] Could not get a .NET 10 SDK automatically.
echo     Install the SDK (x64) from: https://dotnet.microsoft.com/download/dotnet/10.0
echo     then run this script again.
start "" "https://dotnet.microsoft.com/download/dotnet/10.0"
pause
exit /b 1

:BUILD
echo === Building PKHaX (Release, win-x64). Takes a minute or two... ===
dotnet publish PKHeX.WinForms -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
if errorlevel 1 (
  echo [X] Build failed - read the dotnet output above.
  pause
  exit /b 1
)
set "OUTDIR=PKHeX.WinForms\bin\Release\net10.0-windows\win-x64\publish"
if not exist "%OUTDIR%\PKHeX.exe" (
  echo [X] Build finished but PKHeX.exe not found in %OUTDIR%
  pause
  exit /b 1
)
copy /y "%OUTDIR%\PKHeX.exe" "%OUTDIR%\PKHaX.exe" >nul
echo.
echo [OK] PKHaX built:
echo      %CD%\%OUTDIR%\PKHaX.exe
echo Run PKHaX.exe (needs the .NET 10 Desktop Runtime). The "HaX" name enables illegal-edit mode.
pause
