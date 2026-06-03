@echo off
setlocal
title Un-Nerf Compendium - Black 2 / White 2 No Restrictions
echo ============================================================
echo   Black 2 / White 2 : remove ALL Battle Subway + PWT limits
echo   (banlist, Soul Dew, item clause, species clause, 3-mon cap)
echo   You supply your own legally-dumped, DECRYPTED .nds
echo ============================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your Black 2 / White 2 .nds onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0black2_nobanlist.py" "%ROM%"
echo.
pause
