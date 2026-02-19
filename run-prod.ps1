Param(
  [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path "$PSScriptRoot/publish/LBElectronica.Server.dll")) {
  Write-Error 'Publish output not found. Run .\\build.ps1 first.'
}

Set-Location "$PSScriptRoot/publish"
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"
Write-Host "Running production server on http://0.0.0.0:$Port"
dotnet .\LBElectronica.Server.dll
