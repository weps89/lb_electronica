Param(
  [string]$TaskName = "LBElectronica-CloudBackup-Daily",
  [string]$RunAt = "22:00",
  [string]$RemoteName = "gdrive",
  [string]$RemoteFolder = "LBElectronica/backups"
)

$ErrorActionPreference = "Stop"

$scriptPath = (Resolve-Path "$PSScriptRoot/backup-cloud.ps1").Path
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -RemoteName `"$RemoteName`" -RemoteFolder `"$RemoteFolder`""
$trigger = New-ScheduledTaskTrigger -Daily -At $RunAt
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Description "Backup automatico LB Electronica a nube" -Force | Out-Null

Write-Host "Tarea programada creada: $TaskName"
Write-Host "Horario: diario a las $RunAt"
