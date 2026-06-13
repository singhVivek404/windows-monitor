param(
    [string]$OutputPath = '.\Data\services.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$services = Get-CimInstance Win32_Service | Select-Object @{n='Name';e={$_.Name}}, @{n='Status';e={$_.State}}, @{n='StartType';e={$_.StartMode}}
$services | ConvertTo-Json -Depth 3 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
