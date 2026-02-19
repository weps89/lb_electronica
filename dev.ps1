Param(
  [int]$ServerPort = 5080,
  [int]$ClientPort = 5173
)

$ErrorActionPreference = 'Stop'

Write-Host "Starting server on port $ServerPort and client on port $ClientPort..."

$serverJob = Start-Job -ScriptBlock {
  param($port)
  Set-Location "$using:PSScriptRoot/server"
  dotnet run --urls "http://0.0.0.0:$port"
} -ArgumentList $ServerPort

$clientJob = Start-Job -ScriptBlock {
  param($port)
  Set-Location "$using:PSScriptRoot/client"
  npm run dev -- --host --port $port
} -ArgumentList $ClientPort

Write-Host "Press Ctrl+C to stop."

try {
  while ($true) {
    Receive-Job $serverJob -Keep
    Receive-Job $clientJob -Keep
    Start-Sleep -Milliseconds 800
  }
}
finally {
  Stop-Job $serverJob -ErrorAction SilentlyContinue
  Stop-Job $clientJob -ErrorAction SilentlyContinue
  Remove-Job $serverJob -ErrorAction SilentlyContinue
  Remove-Job $clientJob -ErrorAction SilentlyContinue
}
