@echo off
setlocal
title Un-Nerf Compendium - ORAS 510 EV Cap Removal (Battle Maison)
echo ============================================================
echo   Omega Ruby / Alpha Sapphire : remove the 510 EV-total cap
echo   Maxed-EV mons become eligible in the Battle Maison
echo   Works on a DECRYPTED .cia or .3ds you dumped yourself
echo ============================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your decrypted OR/AS .cia or .3ds onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0oras_evcap.py" "%ROM%"
echo.
pause
