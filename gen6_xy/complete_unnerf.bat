@echo off
setlocal
title Un-Nerf Compendium - Pokemon X / Y (UNTESTED beta)
echo ==================================================================
echo   THE UN-NERF COMPENDIUM - Pokemon X / Y  (UNTESTED BETA)
echo   Every X/Y un-nerf baked into a NEW copy of your dump.
echo   Works on a DECRYPTED .3ds (or a .cia whose .code is flat)
echo ==================================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your decrypted X or Y .3ds onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0complete_unnerf.py" "%ROM%"
echo.
pause
