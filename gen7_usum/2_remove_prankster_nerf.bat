@echo off
setlocal enabledelayedexpansion
title Un-Nerf Compendium - Prankster (Ultra Sun / Ultra Moon)
echo ============================================================
echo   THE UN-NERF COMPENDIUM
echo   Prankster : restore effect against Dark-types (pre-Gen7)
echo   Works on Pokemon Ultra Sun OR Ultra Moon (.cia)
echo ============================================================
echo.

set "PY="
where py  >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY (
  echo [!] Python 3 was not found on your PATH.
  echo     Install it from https://www.python.org/downloads/  ^(tick "Add Python to PATH"^)
  echo     then run this file again.
  pause & exit /b 1
)

set "CIA=%~1"
if "%CIA%"=="" set /p CIA=Drag your Ultra Sun/Moon .cia onto this window and press Enter:
set "CIA=%CIA:"=%"

%PY% "%~dp0unnerf.py" --cia "%CIA%" --mode prankster
echo.
pause
