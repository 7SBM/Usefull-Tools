@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "DEL_all_PNG_in_FOLDER.ps1"
exit