Param(
  [int]$Port = 5080,
  [switch]$EnableFirewallRule,
  [switch]$CreateStartupTask
)

$ErrorActionPreference = 'Stop'

function Test-IsAdmin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($id)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$startCmd = Join-Path $appDir 'start-lb-electronica.cmd'
if (-not (Test-Path $startCmd)) {
  throw "No se encontro start-lb-electronica.cmd en $appDir"
}

if (($EnableFirewallRule -or $CreateStartupTask) -and -not (Test-IsAdmin)) {
  throw 'Se requieren permisos de Administrador para aplicar firewall o inicio automatico.'
}

if ($EnableFirewallRule) {
  $ruleName = "LB Electronica TCP $Port"
  $existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
  if ($existing) {
    Remove-NetFirewallRule -DisplayName $ruleName | Out-Null
  }
  New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
  Write-Host "Firewall OK: habilitado puerto TCP $Port"
}

if ($CreateStartupTask) {
  $taskName = 'LB Electronica AutoStart'
  $escaped = '"' + $startCmd + '" ' + $Port
  schtasks /Create /F /SC ONLOGON /RL HIGHEST /TN "$taskName" /TR "$escaped" | Out-Null
  Write-Host 'Inicio automatico OK: tarea programada creada.'
}

if (-not $EnableFirewallRule -and -not $CreateStartupTask) {
  Write-Host 'Sin cambios: no se seleccionaron opciones adicionales.'
}

Write-Host 'Configuracion finalizada.'
