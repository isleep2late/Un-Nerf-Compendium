@echo off
REM Gen 6/7 Forme Persistence - drag your decrypted .cia or .3ds onto this file,
REM or run:  apply_formepersist.bat  "C:\path\to\YourGame.cia"
setlocal
if "%~1"=="" (
  echo Drag a decrypted .cia or .3ds onto this .bat, or pass the path as an argument.
  echo   Mega/Primal persist is auto-located for X/Y, OR/AS, S/M, US/UM.
  echo   Add  --full  for the complete verified forme table on US/UM and OR/AS.
  pause
  exit /b
)
python "%~dp0formepersist.py" "%~1" %2 %3
echo.
pause
