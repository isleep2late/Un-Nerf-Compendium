@echo off
setlocal
title Un-Nerf Compendium - every un-nerf in one pass
echo ==================================================================
echo   THE UN-NERF COMPENDIUM - COMPLETE
echo   Every un-nerf for this game, baked into a NEW copy of your dump.
echo   Omega Ruby / Alpha Sapphire: use the copy in gen6_oras
echo   Ultra Sun / Ultra Moon:      use the copy in gen7_usum
echo   Works on a DECRYPTED .3ds (or a .cia whose .code is flat)
echo ==================================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your decrypted .3ds or .cia onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0complete_unnerf.py" "%ROM%"
echo.
pause
