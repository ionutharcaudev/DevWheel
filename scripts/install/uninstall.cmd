@echo off
rem Double-click to remove DevWheel for the current user (settings are kept).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" %*
pause
