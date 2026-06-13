param(
    [string]$OutputPath = '.\Data\disk.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$disks = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object {
    [PSCustomObject]@{
        Drive = $_.DeviceID
        TotalSizeGB = if ($_.Size) { [math]::Round($_.Size/1GB,2) } else { 0 }
        FreeSpaceGB = if ($_.FreeSpace) { [math]::Round($_.FreeSpace/1GB,2) } else { 0 }
        UsedPercentage = if ($_.Size -and $_.FreeSpace) { [math]::Round((($_.Size - $_.FreeSpace)/$_.Size)*100,2) } else { 0 }
    }
}

$disks | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
