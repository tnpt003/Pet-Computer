@echo off
cd /d "%~dp0"
echo === Do lan 1: cho phep noi thang ===
EosProbe.exe host
echo.
echo === Do lan 2: ep di qua relay ===
EosProbe.exe host --relay force
pause
