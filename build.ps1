$ErrorActionPreference = 'Stop'

Write-Host 'Building client...'
Set-Location "$PSScriptRoot/client"
npm install
npm run build

Write-Host 'Copying client dist to server/wwwroot...'
Set-Location "$PSScriptRoot"
if (Test-Path "$PSScriptRoot/server/wwwroot") { Remove-Item "$PSScriptRoot/server/wwwroot" -Recurse -Force }
New-Item -ItemType Directory -Path "$PSScriptRoot/server/wwwroot" | Out-Null
Copy-Item "$PSScriptRoot/client/dist/*" "$PSScriptRoot/server/wwwroot" -Recurse -Force

Write-Host 'Publishing server...'
Set-Location "$PSScriptRoot/server"
dotnet restore
dotnet publish -c Release -o "$PSScriptRoot/publish"

Write-Host "Done. Output: $PSScriptRoot/publish"
