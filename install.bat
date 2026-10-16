@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" > "%~dp0install-log.txt" 2>&1
type "%~dp0install-log.txt"
echo.
pause
