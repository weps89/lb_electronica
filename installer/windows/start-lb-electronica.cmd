@echo off
setlocal
set "APPDIR=%~dp0"
set "PORT=5080"
if not "%~1"=="" set "PORT=%~1"
set "LOGDIR=%APPDIR%logs"
set "LOGFILE=%LOGDIR%\server.log"

if not exist "%LOGDIR%" mkdir "%LOGDIR%" >nul 2>&1

for /f "tokens=5" %%a in ('netstat -ano ^| findstr /r /c:":%PORT% .*LISTENING"') do (
  set "EXISTING_PID=%%a"
)

if defined EXISTING_PID (
  echo LB Electronica ya esta en ejecucion en el puerto %PORT% (PID %EXISTING_PID%).
  start "" "http://localhost:%PORT%"
  exit /b 0
)

cd /d "%APPDIR%"
if not exist "%APPDIR%LBElectronica.Server.exe" (
  echo No se encontro LBElectronica.Server.exe en %APPDIR%
  exit /b 1
)

echo Iniciando LB Electronica en http://0.0.0.0:%PORT% ...
start "LB Electronica Server" /min cmd /c "\"%APPDIR%LBElectronica.Server.exe\" --urls \"http://0.0.0.0:%PORT%\" >> \"%LOGFILE%\" 2>&1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; for($i=0;$i -lt 20;$i++){ try { $r=Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost:%PORT%/api/health' -TimeoutSec 2; if($r.StatusCode -ge 200 -and $r.StatusCode -lt 300){$ok=$true; break} } catch {}; Start-Sleep -Milliseconds 500 }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
  echo No se pudo validar el inicio del servidor. Revise: "%LOGFILE%"
  exit /b 1
)

start "" "http://localhost:%PORT%"
exit /b 0
