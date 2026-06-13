<#
build-installer.ps1
Full pipeline: dotnet publish -> Inno Setup compile -> WorkstationAuditorSetup.exe

Prerequisites:
  * .NET SDK 10+ (dotnet in PATH)
  * Inno Setup 6  (https://jrsoftware.org/isinfo.php)
    - Default install path: C:\Program Files (x86)\Inno Setup 6\ISCC.exe
    - or set $env:ISCC_PATH to the full path of ISCC.exe

Usage (from any directory):
  .\scripts\build-installer.ps1
  .\scripts\build-installer.ps1 -SkipPublish    # rebuild installer only
  .\scripts\build-installer.ps1 -Runtime win-x86 # for 32-bit target
#>

param(
    [string]$Runtime      = "win-x64",
    [string]$Config       = "Release",
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$proj       = Join-Path $repoRoot "Auditor.UI\Auditor.UI.csproj"
$publishDir = Join-Path $repoRoot "publish"
$distDir    = Join-Path $repoRoot "dist"
$issFile    = Join-Path $repoRoot "installer\setup.iss"

# ====== Locate ISCC (Inno Setup Compiler) =========================================
function Find-ISCC {
    if ($env:ISCC_PATH -and (Test-Path $env:ISCC_PATH)) { return $env:ISCC_PATH }
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    # Try PATH
    $fromPath = Get-Command ISCC -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }
    return $null
}

$iscc = Find-ISCC
if (-not $iscc) {
    Write-Host ""
    Write-Host "===============================================================" -ForegroundColor Yellow
    Write-Host " Inno Setup not found!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host " Download and install Inno Setup 6 from:" -ForegroundColor Cyan
    Write-Host "   https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host ""
    Write-Host " Then re-run this script, or set:" -ForegroundColor White
    Write-Host '   $env:ISCC_PATH = "C:\path\to\ISCC.exe"' -ForegroundColor White
    Write-Host "===============================================================" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "Using ISCC: $iscc" -ForegroundColor Cyan
Write-Host ""

# ====== Step 1: dotnet publish =====================================================
if (-not $SkipPublish) {
    Write-Host "Step 1/2 - Publishing self-contained EXE..." -ForegroundColor Cyan
    Write-Host "  Project : $proj"
    Write-Host "  Runtime : $Runtime"
    Write-Host "  Output  : $publishDir"
    Write-Host ""

    Push-Location $repoRoot
    try {
        dotnet publish $proj `
            -c $Config `
            -r $Runtime `
            --self-contained true `
            /p:PublishSingleFile=true `
            /p:PublishTrimmed=false `
            -o $publishDir

        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed (exit $LASTEXITCODE)"
            exit $LASTEXITCODE
        }
    } finally { Pop-Location }

    # Copy PS1 collectors alongside the EXE
    Write-Host ""
    Write-Host "Copying PowerShell collectors to publish folder..." -ForegroundColor Cyan
    Get-ChildItem -Path $repoRoot -Filter "*.ps1" -File | ForEach-Object {
        Copy-Item $_.FullName -Destination $publishDir -Force
        Write-Host "  Copied: $($_.Name)"
    }
    Write-Host ""
} else {
    Write-Host "Skipping dotnet publish (SkipPublish flag set)" -ForegroundColor Yellow
    Write-Host ""
}

# ====== Step 2: Inno Setup compile =================================================
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory $distDir | Out-Null }

Write-Host "Step 2/2 - Compiling installer..." -ForegroundColor Cyan
Write-Host "  Script  : $issFile"
Write-Host "  Output  : $distDir"
Write-Host ""

& $iscc $issFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "ISCC failed (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

$setup = Join-Path $distDir "WorkstationAuditorSetup.exe"
Write-Host ""
if (Test-Path $setup) {
    $sizeMB = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host "===============================================================" -ForegroundColor Green
    Write-Host " Installer ready!  ($sizeMB MB)" -ForegroundColor Green
    Write-Host " $setup" -ForegroundColor White
    Write-Host "===============================================================" -ForegroundColor Green
} else {
    Write-Warning "Setup EXE not found at expected path: $setup"
}
