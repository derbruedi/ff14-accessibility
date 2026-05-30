@echo off
echo ============================================
echo  FF14 Accessibility - Dalamud Setup
echo ============================================
echo.

REM Typischer XIVLauncher Pfad
set DALAMUD_PATH=%APPDATA%\XIVLauncher\addon\Hooks\dev

echo Suche Dalamud in: %DALAMUD_PATH%
echo.

if not exist "%DALAMUD_PATH%\Dalamud.dll" (
    echo [FEHLER] Dalamud.dll nicht gefunden unter:
    echo %DALAMUD_PATH%
    echo.
    echo Bitte den korrekten Pfad eingeben (wo Dalamud.dll liegt):
    set /p DALAMUD_PATH="Pfad: "

    if not exist "%DALAMUD_PATH%\Dalamud.dll" (
        echo [FEHLER] Dalamud.dll immer noch nicht gefunden. Abbruch.
        pause
        exit /b 1
    )
)

echo [OK] Dalamud.dll gefunden.
echo.
echo Setze DALAMUD_HOME = %DALAMUD_PATH%
echo.

REM Umgebungsvariable dauerhaft fuer den aktuellen Benutzer setzen
setx DALAMUD_HOME "%DALAMUD_PATH%"

echo.
echo ============================================
echo  Fertig!
echo  DALAMUD_HOME wurde gesetzt auf:
echo  %DALAMUD_PATH%
echo ============================================
echo.
echo WICHTIG: Visual Studio / Rider neu starten
echo damit die Variable wirksam wird!
echo.
pause
