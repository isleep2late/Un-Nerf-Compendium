@echo off
setlocal
title Un-Nerf Compendium - Platinum Frontier Clause Removal
echo ============================================================
echo   Pokemon Platinum : remove Battle Frontier SPECIES + ITEM
echo   clauses (duplicate Pokemon / duplicate items allowed).
echo   Decrypted .nds you dumped. Composes with the other two
echo   Platinum patches - run in any order.
echo ============================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "ROM=%~1"
if "%ROM%"=="" set /p ROM=Drag your Platinum .nds onto this window and press Enter:
set "ROM=%ROM:"=%"
%PY% "%~dp0platinum_clauses.py" "%ROM%"
echo.
pause
