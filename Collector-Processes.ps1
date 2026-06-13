<# Collector-Processes.ps1
Collects running processes and writes processes.json
#>

param(
    [string]$OutputPath = '.\Data\processes.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$procs = Get-Process | Select-Object @{n='Name';e={$_.ProcessName}}, @{n='Pid';e={$_.Id}}, @{n='CPU';e={$(if ($_.CPU) { [math]::Round($_.CPU,2) } else { 0 })}}, @{n='MemoryMb';e={[math]::Round($_.WorkingSet/1MB,2)}} | Sort-Object -Property CPU -Descending

$procs | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
