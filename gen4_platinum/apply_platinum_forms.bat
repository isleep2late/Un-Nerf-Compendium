@echo off
setlocal
title Un-Nerf Compendium - Platinum Frontier Form Restrictions
echo ============================================================
echo   Pokemon Platinum : remove Battle Frontier FORM restrictions
echo   (Origin Giratina, Rotom forms, Sky Shaymin stop reverting).
echo   Decrypted .nds you dumped. Needs ndspy (pip install ndspy).
echo   Composes with platinum_nobanlist.py - run either order.
echo   Credit: SmolJoltik, Project Pokemon topic 67882.
echo ============================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your Platinum .nds onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0platinum_forms.py" "%ROM%"
echo.
pause
