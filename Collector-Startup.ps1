param(
    [string]$OutputPath = '.\Data\startup.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$result = @()

$regPaths = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
    'HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
)

foreach ($p in $regPaths) {
    if (Test-Path $p) {
        $props = Get-ItemProperty -Path $p -ErrorAction SilentlyContinue
        if ($props) {
            foreach ($prop in $props.PSObject.Properties) {
                if ($prop.Name -in @('PSPath','PSParentPath','PSChildName','PSDrive','PSProvider')) { continue }
                $result += [PSCustomObject]@{ Name = $prop.Name; Location = $p; Command = $prop.Value }
            }
        }
    }
}

$programData = if ($env:PROGRAMDATA -is [System.Array]) { $env:PROGRAMDATA[0] } else { $env:PROGRAMDATA }
$appData = if ($env:APPDATA -is [System.Array]) { $env:APPDATA[0] } else { $env:APPDATA }

$startupFolders = @(
    Join-Path $programData 'Microsoft\Windows\Start Menu\Programs\Startup'
    Join-Path $appData 'Microsoft\Windows\Start Menu\Programs\Startup'
)

$shell = New-Object -ComObject WScript.Shell
foreach ($folder in $startupFolders) {
    if (Test-Path $folder) {
        Get-ChildItem -Path $folder -File -ErrorAction SilentlyContinue | ForEach-Object {
            $file = $_
            $cmd = $file.FullName
            if ($file.Extension -ieq '.lnk') {
                try { $lnk = $shell.CreateShortcut($file.FullName); $cmd = $lnk.TargetPath } catch {}
            }
            $result += [PSCustomObject]@{ Name = $file.BaseName; Location = $file.FullName; Command = $cmd }
        }
    }
}

$result | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
