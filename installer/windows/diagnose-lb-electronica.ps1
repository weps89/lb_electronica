Param(
  [int]$Port = 5080
)

$ErrorActionPreference = 'Continue'

$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $appDir 'LBElectronica.Server.exe'
$db = Join-Path $appDir 'lb_electronica.db'
$healthUrl = "http://localhost:$Port/api/health"

Write-Host '=== Diagnostico LB Electronica ==='
Write-Host "Directorio: $appDir"

if (Test-Path $exe) { Write-Host '[OK] Ejecutable encontrado.' } else { Write-Host '[ERROR] Falta LBElectronica.Server.exe' }
if (Test-Path $db) { Write-Host '[OK] Base de datos encontrada.' } else { Write-Host '[WARN] Base de datos aun no creada (se genera en primer inicio).' }

$portListening = $false
try {
  $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop
  if ($conn) { $portListening = $true }
} catch {}

if ($portListening) { Write-Host "[OK] Puerto $Port en escucha." } else { Write-Host "[WARN] Puerto $Port no esta en escucha." }

try {
  $r = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 4
  if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) {
    Write-Host "[OK] Health endpoint responde ($($r.StatusCode))."
  } else {
    Write-Host "[WARN] Health endpoint responde con estado $($r.StatusCode)."
  }
} catch {
  Write-Host '[WARN] No se pudo consultar /api/health. Verifique que el servidor este iniciado.'
}

Write-Host '=== Fin diagnostico ==='
