param(
    [string]$OutputPath = '.\Data\software.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$keys = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
)

$result = @()
foreach ($k in $keys) {
    try {
        Get-ItemProperty -Path $k -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.DisplayName) {
                $result += [PSCustomObject]@{
                    Name = $_.DisplayName
                    Version = $_.DisplayVersion
                    Publisher = $_.Publisher
                }
            }
        }
    } catch {}
}

$result = $result | Sort-Object Name -Unique
$result | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
