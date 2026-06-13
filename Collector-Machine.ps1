<# Collector-Machine.ps1
Collects machine details including OS-level memory usage and writes machine.json.
Memory stats are read from Win32_OperatingSystem (kernel-accurate, not process-sum).
#>

param(
    [string]$OutputPath = '.\Data\machine.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) {
    New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null
}

$os  = Get-CimInstance Win32_OperatingSystem
$cs  = Get-CimInstance Win32_ComputerSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1

# FreePhysicalMemory is in KB; dividing by 1MB (=1,048,576) converts KB -> GB
$totalGB = [math]::Round($cs.TotalPhysicalMemory / 1GB, 2)
$freeGB  = [math]::Round($os.FreePhysicalMemory  / 1MB, 2)

$machine = [PSCustomObject]@{
    ComputerName       = $env:COMPUTERNAME
    OSVersion          = $os.Caption
    TotalMemoryGB      = $totalGB
    FreeMemoryGB       = $freeGB
    UsedMemoryGB       = [math]::Round($totalGB - $freeGB, 2)
    MemoryUsedPercent  = if ($totalGB -gt 0) { [math]::Round((($totalGB - $freeGB) / $totalGB) * 100, 1) } else { 0 }
    CurrentUser        = $env:USERNAME
    BootTime           = $os.LastBootUpTime
    CPUName            = $cpu.Name
    CPUCores           = $cpu.NumberOfCores
    CPULogicalProcs    = $cpu.NumberOfLogicalProcessors
}

$machine | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
