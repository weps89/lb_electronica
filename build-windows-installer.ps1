Param(
  [string]$Version = '1.0.0',
  [string]$Runtime = 'win-x64',
  [switch]$SkipClientInstall
)

$ErrorActionPreference = 'Stop'

function Require-Command([string]$Name) {
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "No se encontro el comando requerido: $Name"
  }
}

Require-Command dotnet
Require-Command npm

$root = $PSScriptRoot
$clientDir = Join-Path $root 'client'
$serverDir = Join-Path $root 'server'
$outRoot = Join-Path $root 'artifacts/win-installer'
$appOut = Join-Path $outRoot 'app'

Write-Host "[1/6] Limpiando salida anterior..."
if (Test-Path $outRoot) { Remove-Item $outRoot -Recurse -Force }
New-Item -ItemType Directory -Path $appOut | Out-Null

Write-Host "[2/6] Compilando frontend..."
Push-Location $clientDir
if (-not $SkipClientInstall) {
  if (Test-Path (Join-Path $clientDir 'package-lock.json')) { npm ci }
  else { npm install }
}
npm run build
Pop-Location

Write-Host "[3/6] Copiando frontend a server/wwwroot..."
$wwwroot = Join-Path $serverDir 'wwwroot'
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Path $wwwroot | Out-Null
Copy-Item (Join-Path $clientDir 'dist/*') $wwwroot -Recurse -Force

Write-Host "[4/6] Publicando backend self-contained ($Runtime)..."
Push-Location $serverDir
dotnet restore
dotnet publish -c Release -r $Runtime --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:PublishTrimmed=false `
  -o $appOut
Pop-Location

Write-Host "[5/6] Copiando scripts de operacion y backup..."
Copy-Item (Join-Path $root 'scripts') (Join-Path $appOut 'scripts') -Recurse -Force
Copy-Item (Join-Path $root 'installer/windows/start-lb-electronica.cmd') $appOut -Force
Copy-Item (Join-Path $root 'installer/windows/stop-lb-electronica.cmd') $appOut -Force
Copy-Item (Join-Path $root 'installer/windows/configure-lb-electronica.ps1') $appOut -Force
Copy-Item (Join-Path $root 'installer/windows/diagnose-lb-electronica.ps1') $appOut -Force

if (-not (Test-Path (Join-Path $appOut 'LBElectronica.Server.exe'))) {
  throw 'Publicacion incompleta: no se genero LBElectronica.Server.exe'
}
if (-not (Test-Path (Join-Path $appOut 'wwwroot/index.html'))) {
  throw 'Publicacion incompleta: no se encontro wwwroot/index.html'
}

$readme = @"
LB Electronica - Paquete Windows
================================

1) Ejecutar start-lb-electronica.cmd
2) Abrir http://localhost:5080
3) Usuario inicial: admin
4) Password inicial: admin123!

Notas:
- Para configurar LAN/firewall e inicio automatico:
  powershell -ExecutionPolicy Bypass -File .\configure-lb-electronica.ps1 -EnableFirewallRule -CreateStartupTask
- Para detener servidor: stop-lb-electronica.cmd
- Diagnostico rapido:
  powershell -ExecutionPolicy Bypass -File .\diagnose-lb-electronica.ps1
- Backups cloud: carpeta scripts/
"@
Set-Content -Path (Join-Path $appOut 'LEEME-WINDOWS.txt') -Value $readme -Encoding UTF8

Write-Host "[6/6] Intentando compilar instalador Inno Setup..."
$iss = Join-Path $root 'installer/windows/LBElectronica.iss'
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
  $known = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
  )
  foreach ($k in $known) {
    if (Test-Path $k) { $iscc = @{ Source = $k }; break }
  }
}

if ($iscc) {
  $isccPath = $iscc.Source
  & $isccPath "/DMyAppVersion=$Version" "/DMySourceDir=$appOut" $iss
  Write-Host "Instalador generado en: $outRoot"
} else {
  Write-Warning 'ISCC.exe no encontrado. Se genero solo el paquete portable en artifacts/win-installer/app.'
  Write-Host 'Para generar setup .exe, instale Inno Setup 6 y vuelva a ejecutar este script.'
}

Write-Host "Proceso finalizado."
