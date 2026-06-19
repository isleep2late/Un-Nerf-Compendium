@echo off
setlocal enabledelayedexpansion
title Un-Nerf Compendium - BDSP Battle Tower No Ban List (mod builder)
echo ============================================================
echo   BDSP : build a LayeredFS mod that removes the Battle Tower
echo   banned-Pokemon list. You supply your own dumped
echo   global-metadata.dat (romfs\Data\Managed\Metadata\).
echo   Does NOT remove the species/item clause (in progress).
echo ============================================================
echo.
set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY ( where python >nul 2>&1 && set "PY=python" )
if not defined PY ( echo [!] Python 3 not found on PATH. Install from python.org. & pause & exit /b 1 )
set "MD=%~1"
if "%MD%"=="" set /p MD=Drag your global-metadata.dat onto this window and press Enter:
set "MD=%MD:"=%"

REM build the mod folder structure: <mod>\romfs\Data\Managed\Metadata\global-metadata.dat
set "MOD=%~dp0BDSP_NoBanList_Mod\romfs\Data\Managed\Metadata"
if not exist "%MOD%" mkdir "%MOD%"
%PY% "%~dp0bdsp_nobanlist.py" "%MD%" --out "%MOD%\global-metadata.dat"
if errorlevel 1 ( echo [!] patch failed. & pause & exit /b 1 )
echo.
echo ============================================================
echo  Mod built at:  %~dp0BDSP_NoBanList_Mod
echo.
echo  Install it by copying the BDSP_NoBanList_Mod folder into your
echo  emulator's mod directory for your game:
echo.
echo   Ryujinx:  Ryujinx\mods\contents\^<TitleID^>\
echo   Yuzu:     yuzu\load\^<TitleID^>\
echo.
echo   Title IDs:  Brilliant Diamond = 0100000011D90000
echo               Shining Pearl     = 010018E011D92000
echo.
echo  Then enable the mod in the emulator's game properties / mods list.
echo ============================================================
echo.
pause
