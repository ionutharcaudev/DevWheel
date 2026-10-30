@echo off
rem Double-click to install DevWheel for the current user and start it with Windows.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
pause
