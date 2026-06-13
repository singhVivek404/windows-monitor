<#
Collector-DevEnv.ps1
Developer environment audit: WSL2 virtual disks, Docker, package manager caches,
zombie runtime processes, and PATH / key-tool availability.
#>

param(
    [string]$OutputPath = '.\Data\devenv.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'   # Don't abort on individual check failures

if (-not (Test-Path (Split-Path $OutputPath))) {
    New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null
}

# === 1. WSL2 Virtual Disk Detection ============================================
$wslDetected  = $false
$wslDisks     = @()
try {
    $packagesDir = Join-Path $env:LOCALAPPDATA 'Packages'
    if (Test-Path $packagesDir) {
        Get-ChildItem $packagesDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $vhdx = Join-Path $_.FullName 'LocalState\ext4.vhdx'
            if (Test-Path $vhdx) {
                $wslDetected = $true
                $sizeGB = [math]::Round((Get-Item $vhdx -ErrorAction SilentlyContinue).Length / 1GB, 2)
                # Friendly name: strip the Windows package prefix noise
                $distroName = $_.Name -replace '^CanonicalGroupLimited\.', '' `
                                      -replace '^WhitewaterFoundry-Ltd\.', '' `
                                      -replace '_.*$', ''
                $wslDisks += [PSCustomObject]@{
                    Distribution = $distroName
                    SizeGB       = $sizeGB
                    VhdxPath     = $vhdx
                }
            }
        }
    }
} catch {}

# === 2. Docker Detection ========================================================
$dockerDetected    = $false
$dockerContainers  = @()
$dockerImages      = @()
try {
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $dockerDetected = $true
        $raw = docker ps --format "{{.Names}}|{{.Image}}|{{.Status}}" 2>$null
        if ($raw) {
            foreach ($line in ($raw -split "`n")) {
                $parts = $line -split '\|'
                if ($parts.Count -ge 3) {
                    $dockerContainers += [PSCustomObject]@{
                        Name   = $parts[0].Trim()
                        Image  = $parts[1].Trim()
                        Status = $parts[2].Trim()
                    }
                }
            }
        }
        $imgRaw = docker images --format "{{.Repository}}:{{.Tag}}|{{.Size}}" 2>$null
        if ($imgRaw) {
            foreach ($line in ($imgRaw -split "`n" | Select-Object -First 20)) {
                $parts = $line -split '\|'
                if ($parts.Count -ge 2) {
                    $dockerImages += [PSCustomObject]@{ Name = $parts[0].Trim(); Size = $parts[1].Trim() }
                }
            }
        }
    }
} catch {}

# === 3. Developer Package Cache Sizes ==========================================
function Get-DirSizeGB([string]$path) {
    try {
        if (-not (Test-Path $path)) { return 0.0 }
        $bytes = (Get-ChildItem $path -Recurse -File -ErrorAction SilentlyContinue |
                  Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
        return [math]::Round($bytes / 1GB, 2)
    } catch { return 0.0 }
}

$cacheLocations = @(
    [PSCustomObject]@{ Name = 'npm cache';        Path = Join-Path $env:APPDATA      'npm-cache' },
    [PSCustomObject]@{ Name = 'NuGet packages';   Path = Join-Path $env:USERPROFILE  '.nuget\packages' },
    [PSCustomObject]@{ Name = 'Gradle caches';    Path = Join-Path $env:USERPROFILE  '.gradle\caches' },
    [PSCustomObject]@{ Name = 'Maven (.m2)';      Path = Join-Path $env:USERPROFILE  '.m2\repository' },
    [PSCustomObject]@{ Name = 'pip cache';        Path = Join-Path $env:LOCALAPPDATA 'pip\Cache' },
    [PSCustomObject]@{ Name = 'Cargo registry';   Path = Join-Path $env:USERPROFILE  '.cargo\registry' },
    [PSCustomObject]@{ Name = 'pnpm store';       Path = Join-Path $env:LOCALAPPDATA 'pnpm\store' },
    [PSCustomObject]@{ Name = 'Yarn cache';       Path = Join-Path $env:LOCALAPPDATA 'Yarn\Cache' },
    [PSCustomObject]@{ Name = 'Go module cache';  Path = Join-Path $env:USERPROFILE  'go\pkg\mod' }
)

$devCaches = @()
foreach ($loc in $cacheLocations) {
    if (Test-Path $loc.Path) {
        $sizeGB = Get-DirSizeGB $loc.Path
        $devCaches += [PSCustomObject]@{
            Name   = $loc.Name
            SizeGB = $sizeGB
            Path   = $loc.Path
        }
    }
}
$totalCacheGB = [math]::Round(($devCaches | Measure-Object -Property SizeGB -Sum).Sum, 2)

# === 4. Zombie / Orphaned Developer Processes =================================
$zombieTargets = @('node', 'dotnet', 'msbuild', 'java', 'javac', 'gradle',
                   'python', 'ruby', 'esbuild', 'tsc', 'webpack')
$zombieProcs = @()
try {
    $allProcs = Get-Process -ErrorAction SilentlyContinue
    foreach ($target in $zombieTargets) {
        $allProcs | Where-Object { $_.ProcessName -ieq $target } | ForEach-Object {
            $startStr = $null
            try { $startStr = $_.StartTime.ToString('o') } catch {}
            $zombieProcs += [PSCustomObject]@{
                ProcessName = $_.ProcessName
                Pid         = $_.Id
                MemoryMb    = [math]::Round($_.WorkingSet / 1MB, 2)
                StartTime   = $startStr
            }
        }
    }
} catch {}

# === 5. PATH & Key-Tool Availability ==========================================
$toolsToCheck = @('git', 'docker', 'node', 'npm', 'dotnet', 'python',
                  'java', 'code', 'kubectl', 'go', 'cargo', 'rustc')
$pathTools = @()
foreach ($tool in $toolsToCheck) {
    $cmd = Get-Command $tool -ErrorAction SilentlyContinue
    $pathTools += [PSCustomObject]@{
        ToolName = $tool
        Found    = ($null -ne $cmd)
        Path     = if ($cmd) { $cmd.Source } else { $null }
        Version  = $null   # populated below for key tools
    }
}

# Grab versions for the tools that are cheap to check
try { ($pathTools | Where-Object ToolName -eq 'git'   )[0].Version = (git --version 2>$null) -replace 'git version ', '' } catch {}
try { ($pathTools | Where-Object ToolName -eq 'node'  )[0].Version = (node --version 2>$null).TrimStart('v') } catch {}
try { ($pathTools | Where-Object ToolName -eq 'dotnet')[0].Version = (dotnet --version 2>$null) } catch {}

$pathLength      = $env:PATH.Length
$longPathsEnabled = $false
try {
    $lpe = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' `
            -Name LongPathsEnabled -ErrorAction SilentlyContinue).LongPathsEnabled
    $longPathsEnabled = ($lpe -eq 1)
} catch {}

# === Assemble & Write ==========================================================
$result = [PSCustomObject]@{
    WslDetected       = $wslDetected
    WslDisks          = $wslDisks
    DockerDetected    = $dockerDetected
    DockerContainers  = $dockerContainers
    DockerImages      = $dockerImages
    DevCaches         = $devCaches
    TotalDevCacheSizeGB = $totalCacheGB
    ZombieProcesses   = $zombieProcs
    PathTools         = $pathTools
    PathLength        = $pathLength
    LongPathsEnabled  = $longPathsEnabled
}

$result | ConvertTo-Json -Depth 6 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
