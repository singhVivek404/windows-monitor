param(
    [string]$OutputPath = '.\Data\network.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) { New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null }

$result = @()
if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
    Get-NetTCPConnection -ErrorAction SilentlyContinue | ForEach-Object {
        $proc = $null
        try { $proc = (Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName } catch {}
        $result += [PSCustomObject]@{
            LocalAddress = "$($_.LocalAddress):$($_.LocalPort)"
            RemoteAddress = "$($_.RemoteAddress):$($_.RemotePort)"
            State = $_.State
            Process = $proc
        }
    }
} else {
    $lines = netstat -ano | Select-String -Pattern 'TCP'
    foreach ($line in $lines) {
        $parts = ($line -split '\s+') | Where-Object { $_ -ne '' }
        if ($parts.Length -ge 5) {
            $local = $parts[1]; $remote = $parts[2]; $state = $parts[3]; $pid = $parts[4]
            $proc = $null
            try { $proc = (Get-Process -Id [int]$pid -ErrorAction SilentlyContinue).ProcessName } catch {}
            $result += [PSCustomObject]@{
                LocalAddress = $local
                RemoteAddress = $remote
                State = $state
                Process = $proc
            }
        }
    }
}

$result | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
