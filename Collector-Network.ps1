param(
    [string]$OutputPath = '.\Data\network.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Split-Path $OutputPath))) {
    New-Item -ItemType Directory -Path (Split-Path $OutputPath) | Out-Null
}

# Build a process-name lookup table ONCE (O(n) instead of O(n*m))
# This fixes the original bug where Get-Process was called per-socket causing
# severe performance degradation on machines with hundreds of connections.
$procMap = @{}
Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
    if (-not $procMap.ContainsKey($_.Id)) {
        $procMap[$_.Id] = $_.ProcessName
    }
}

$result = @()

if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
    Get-NetTCPConnection -ErrorAction SilentlyContinue | ForEach-Object {
        $procName = $procMap[[int]$_.OwningProcess]   # O(1) hash lookup
        $result += [PSCustomObject]@{
            LocalAddress  = "$($_.LocalAddress):$($_.LocalPort)"
            RemoteAddress = "$($_.RemoteAddress):$($_.RemotePort)"
            State         = $_.State.ToString()
            Process       = $procName
        }
    }
} else {
    # Fallback: parse netstat -ano
    $lines = netstat -ano | Select-String -Pattern 'TCP'
    foreach ($line in $lines) {
        $parts = ($line -split '\s+') | Where-Object { $_ -ne '' }
        if ($parts.Length -ge 5) {
            $local  = $parts[1]
            $remote = $parts[2]
            $state  = $parts[3]
            $pid    = $parts[4]
            $procName = $null
            if ($pid -match '^\d+$') { $procName = $procMap[[int]$pid] }
            $result += [PSCustomObject]@{
                LocalAddress  = $local
                RemoteAddress = $remote
                State         = $state
                Process       = $procName
            }
        }
    }
}

$result | ConvertTo-Json -Depth 4 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
