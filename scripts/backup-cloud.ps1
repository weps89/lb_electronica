Param(
  [string]$DbPath = "",
  [string]$BackupDir = "",
  [string]$RemoteName = "gdrive",
  [string]$RemoteFolder = "LBElectronica/backups",
  [int]$KeepLocalDays = 30,
  [int]$KeepRemoteDays = 90
)

$ErrorActionPreference = "Stop"

function Resolve-DbPath {
  param([string]$Value)
  if ($Value -and (Test-Path $Value)) { return (Resolve-Path $Value).Path }

  $candidates = @(
    "$PSScriptRoot/../publish/lb_electronica.db",
    "$PSScriptRoot/../server/lb_electronica.db",
    "$PSScriptRoot/../server/bin/Debug/net8.0/lb_electronica.db",
    "$PSScriptRoot/../server/bin/Release/net8.0/lb_electronica.db"
  )

  foreach ($p in $candidates) {
    if (Test-Path $p) { return (Resolve-Path $p).Path }
  }

  throw "No se encontro la base SQLite. Usa -DbPath para indicar la ruta."
}

function Ensure-Cmd {
  param([string]$Name)
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "No se encontro '$Name'. Instalalo y vuelve a ejecutar."
  }
}

Ensure-Cmd "rclone"

$resolvedDb = Resolve-DbPath -Value $DbPath
$dbDir = Split-Path -Parent $resolvedDb
$dbFile = Split-Path -Leaf $resolvedDb

if (-not $BackupDir) {
  $BackupDir = "$PSScriptRoot/../backups/cloud"
}

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$machine = $env:COMPUTERNAME
$workDir = Join-Path $BackupDir "tmp_$stamp"
$zipPath = Join-Path $BackupDir "lb_electronica_${machine}_$stamp.zip"
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

$filesToCopy = @(
  $resolvedDb,
  "$resolvedDb-wal",
  "$resolvedDb-shm"
) | Where-Object { Test-Path $_ }

foreach ($f in $filesToCopy) {
  Copy-Item $f -Destination $workDir -Force
}

$meta = @{
  system = "LB Electronica"
  createdAt = (Get-Date).ToString("s")
  machine = $machine
  dbFile = $dbFile
  source = $resolvedDb
  files = ($filesToCopy | ForEach-Object { Split-Path $_ -Leaf })
} | ConvertTo-Json -Depth 5

$metaPath = Join-Path $workDir "backup_meta.json"
$meta | Out-File -FilePath $metaPath -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$workDir/*" -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item $workDir -Recurse -Force

Write-Host "Backup local creado: $zipPath"

$remoteTarget = "$RemoteName`:$RemoteFolder"
rclone copy $zipPath $remoteTarget --create-empty-src-dirs --transfers 1 --checkers 2
Write-Host "Backup subido a nube: $remoteTarget"

if ($KeepLocalDays -gt 0) {
  Get-ChildItem $BackupDir -File -Filter "*.zip" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepLocalDays) } |
    Remove-Item -Force
}

if ($KeepRemoteDays -gt 0) {
  rclone delete $remoteTarget --min-age "$KeepRemoteDays"d
}

Write-Host "Listo."
