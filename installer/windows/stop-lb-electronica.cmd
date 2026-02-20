@echo off
setlocal
set "PORT=5080"
if not "%~1"=="" set "PORT=%~1"

set "FOUND=0"
for /f "tokens=5" %%a in ('netstat -ano ^| findstr /r /c:":%PORT% .*LISTENING"') do (
  for /f "skip=3 tokens=1" %%p in ('tasklist /FI "PID eq %%a" /FI "IMAGENAME eq LBElectronica.Server.exe"') do (
    set "FOUND=1"
    echo Deteniendo proceso PID %%a en puerto %PORT%...
    taskkill /PID %%a /F >nul 2>&1
  )
)

if "%FOUND%"=="0" (
  echo No hay servidor LB Electronica escuchando en el puerto %PORT%.
  exit /b 0
)

echo Servidor detenido.
exit /b 0
