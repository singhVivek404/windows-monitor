param(
    [string]$Runtime       = "win-x64",
    [string]$Configuration = "Release"
)

# Always resolve paths relative to the repo root (parent of this script's folder)
$repoRoot = Split-Path -Parent $PSScriptRoot
$proj     = Join-Path $repoRoot "Auditor.UI\Auditor.UI.csproj"
$outDir   = Join-Path $repoRoot "publish"

Write-Host "Publishing $proj as single-file self-contained EXE..."
Write-Host "  Runtime   : $Runtime"
Write-Host "  Config    : $Configuration"
Write-Host "  Output    : $outDir"

Push-Location $repoRoot
try {
    dotnet publish $proj `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:PublishTrimmed=false `
        -o $outDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "Copying PowerShell collector scripts alongside the EXE..."

    $scripts = Get-ChildItem -Path $repoRoot -Filter "*.ps1" -File
    foreach ($s in $scripts) {
        Copy-Item $s.FullName -Destination $outDir -Force
        Write-Host "  Copied: $($s.Name)"
    }

    $scriptsSubDir = Join-Path $repoRoot "scripts"
    if (Test-Path $scriptsSubDir) {
        $destScripts = Join-Path $outDir "scripts"
        if (-not (Test-Path $destScripts)) { New-Item -ItemType Directory $destScripts | Out-Null }
        Copy-Item (Join-Path $scriptsSubDir "*") -Destination $destScripts -Force
    }

    Write-Host ""
    Write-Host "Publish succeeded. Distributable output: $outDir"
    Write-Host "Copy the entire 'publish' folder - the EXE and *.ps1 scripts must stay together."
}
finally {
    Pop-Location
}
