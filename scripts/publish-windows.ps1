param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$proj = "Auditor.UI/Auditor.UI.csproj"
Write-Host "Publishing $proj as single-file self-contained EXE (runtime=$Runtime, config=$Configuration)..."

dotnet publish $proj -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o .\publish

if ($LASTEXITCODE -eq 0) {
    Write-Host "Publish succeeded. Output: publish\"
} else {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
}
