@echo off
cd /d "%~dp0"
echo === Do lan 1: cho phep noi thang ===
set /p CODE=Nhap ma phong lan 1:
EosProbe.exe join %CODE%
echo.
echo === Do lan 2: ep di qua relay (ben host da hien ma moi) ===
set /p CODE=Nhap ma phong lan 2:
EosProbe.exe join %CODE% --relay force
pause
