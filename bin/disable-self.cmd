@echo off
setlocal
rem Double-click entry: disable dsh-external-tools even when DSH is not running.
call "%~dp0dshx.cmd" disable dsh-external-tools
echo.
pause
