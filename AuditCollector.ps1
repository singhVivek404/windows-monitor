<#
AuditCollector.ps1
Orchestrates data collection and writes JSON files to the Data folder.
#>

param(
    [string]$OutputDir = "Data",
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "Running collectors from $scriptDir -> $OutputDir"

$collectors = @('Collector-Machine.ps1','Collector-Processes.ps1','Collector-Services.ps1','Collector-Startup.ps1','Collector-Disk.ps1','Collector-Software.ps1','Collector-Network.ps1')
foreach ($c in $collectors) {
    $path = Join-Path $scriptDir $c
    if (Test-Path $path) {
        try {
            & $path -OutputPath (Join-Path $OutputDir ($c -replace '^Collector-','' -replace '\.ps1$','.json'))
        }
        catch {
            Write-Error "Collector $c failed: $_"
        }
    }
    else {
        Write-Host "Skipping missing collector: $c"
    }
}

Write-Host "All collectors invoked."
