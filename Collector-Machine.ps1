<# Collector-Machine.ps1
Collects machine details and writes machine.json (standalone collector)
#>

param(
    [string]$OutputPath = '.\Data\machine.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$machine = [PSCustomObject]@{
    ComputerName = $env:COMPUTERNAME
    OSVersion = (Get-CimInstance Win32_OperatingSystem).Caption
    TotalMemoryGB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1GB,2)
    CurrentUser = $env:USERNAME
    BootTime = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
    CPUName = (Get-CimInstance Win32_Processor).Name
}

$machine | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
