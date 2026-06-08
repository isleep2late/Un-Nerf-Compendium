@echo off
setlocal
title Un-Nerf Compendium - Platinum No Ban List
echo ============================================================
echo   Pokemon Platinum : remove the Battle Frontier banlist
echo   (banned legendaries can enter). Decrypted .nds you dumped.
echo   Form-restriction removal is a separate manual step - see
echo   PLATINUM_FORMS.md in this folder.
echo ============================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your Platinum .nds onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0platinum_nobanlist.py" "%ROM%"
echo.
pause
