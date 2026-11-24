@echo off
setlocal
set "PS1=%~dp0dshx.ps1"
where pwsh >nul 2>nul
if errorlevel 1 goto windows_powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
goto done
:windows_powershell
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
:done
exit /b %errorlevel%
