Param(
  [string]$BackupZipPath = "",
  [string]$DbPath = "",
  [switch]$FetchLatestFromCloud,
  [string]$RemoteName = "gdrive",
  [string]$RemoteFolder = "LBElectronica/backups"
)

$ErrorActionPreference = "Stop"

function Ensure-Cmd {
  param([string]$Name)
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "No se encontro '$Name'. Instalalo y vuelve a ejecutar."
  }
}

function Resolve-DbPath {
  param([string]$Value)
  if ($Value) { return $Value }
  $candidates = @(
    "$PSScriptRoot/../publish/lb_electronica.db",
    "$PSScriptRoot/../server/lb_electronica.db"
  )
  foreach ($p in $candidates) {
    if (Test-Path $p) { return (Resolve-Path $p).Path }
  }
  return "$PSScriptRoot/../server/lb_electronica.db"
}

$resolvedDb = Resolve-DbPath -Value $DbPath
$dbDir = Split-Path -Parent $resolvedDb
New-Item -ItemType Directory -Path $dbDir -Force | Out-Null

if ($FetchLatestFromCloud) {
  Ensure-Cmd "rclone"
  $tmpDir = "$PSScriptRoot/../backups/restore_tmp"
  New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
  $remote = "$RemoteName`:$RemoteFolder"
  $latest = rclone lsf $remote --files-only --format "tp" | Sort-Object | Select-Object -Last 1
  if (-not $latest) { throw "No hay backups remotos en $remote" }
  $fileName = ($latest -split ";")[1]
  $BackupZipPath = Join-Path $tmpDir $fileName
  rclone copyto "$remote/$fileName" $BackupZipPath
}

if (-not $BackupZipPath -or -not (Test-Path $BackupZipPath)) {
  throw "No se encontro el backup zip. Usa -BackupZipPath o -FetchLatestFromCloud."
}

$extractDir = "$PSScriptRoot/../backups/restore_extract_$(Get-Date -Format yyyyMMdd_HHmmss)"
Expand-Archive -Path $BackupZipPath -DestinationPath $extractDir -Force

$dbCandidate = Join-Path $extractDir "lb_electronica.db"
if (-not (Test-Path $dbCandidate)) {
  $found = Get-ChildItem $extractDir -File -Filter "*.db" | Select-Object -First 1
  if (-not $found) { throw "No se encontro archivo .db dentro del backup." }
  $dbCandidate = $found.FullName
}

$backupCurrent = "$resolvedDb.pre_restore_$(Get-Date -Format yyyyMMdd_HHmmss).bak"
if (Test-Path $resolvedDb) {
  Copy-Item $resolvedDb $backupCurrent -Force
  Write-Host "Copia de seguridad previa: $backupCurrent"
}

Copy-Item $dbCandidate $resolvedDb -Force

$walCandidate = Join-Path $extractDir "lb_electronica.db-wal"
$shmCandidate = Join-Path $extractDir "lb_electronica.db-shm"
if (Test-Path $walCandidate) { Copy-Item $walCandidate "$resolvedDb-wal" -Force }
if (Test-Path $shmCandidate) { Copy-Item $shmCandidate "$resolvedDb-shm" -Force }

Write-Host "Restauracion completada en: $resolvedDb"
Write-Host "IMPORTANTE: iniciar o reiniciar el servidor despues de restaurar."
