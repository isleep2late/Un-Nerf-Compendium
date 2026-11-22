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
set "SRC_COMMIT="
set "SRC_STATE="
if exist PKHAX_SOURCE.txt (
  for /f "usebackq tokens=1,* delims==" %%A in ("PKHAX_SOURCE.txt") do (
    if "%%A"=="is2l-commit" if not defined SRC_COMMIT set "SRC_COMMIT=%%B"
    if "%%A"=="state" if not defined SRC_STATE set "SRC_STATE=%%B"
  )
)
if not defined SRC_COMMIT goto NOSTAMP
set "SRC_TAIL=%SRC_COMMIT:~40%"
set "SRC_LAST=%SRC_COMMIT:~39,1%"
if defined SRC_TAIL goto NOSTAMP
if not defined SRC_LAST goto NOSTAMP
echo %SRC_COMMIT%| findstr /r /x "[0123456789abcdef]*" >nul
if errorlevel 1 goto NOSTAMP
set "STAMP=is2l.%SRC_COMMIT%"
if not "%SRC_STATE%"=="clean" set "STAMP=%STAMP%.dirty"
echo === Building PKHaX (Release, win-x64, source %STAMP%). Takes a minute or two... ===
dotnet publish PKHeX.WinForms -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false "-p:SourceRevisionId=%STAMP%"
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
exit /b 0

:NOSTAMP
echo [X] PKHAX_SOURCE.txt names no is2l commit, so the build cannot be stamped with its source.
echo     Run ./build.sh pkhax-export ^<this clone^> from the is2l worktree first.
pause
exit /b 1
