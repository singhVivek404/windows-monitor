<#
diagnostics.ps1
Collects simple system diagnostics and emits JSON.
#>

$ErrorActionPreference = 'Stop'

$cpu = Get-CimInstance Win32_Processor | Select-Object @{n='Name';e={$_.Name}}, @{n='LoadPercentage';e={[int]$_.LoadPercentage}}

$mem = Get-CimInstance Win32_OperatingSystem | Select-Object @{n='TotalMB';e={[math]::Round($_.TotalVisibleMemorySize/1024)}}, @{n='FreeMB';e={[math]::Round($_.FreePhysicalMemory/1024)}}, @{n='TotalVisibleMemorySize';e={$_.TotalVisibleMemorySize}}, @{n='FreePhysicalMemory';e={$_.FreePhysicalMemory}}

$processes = Get-Process | Sort-Object -Property CPU -Descending | Select-Object -First 10 | ForEach-Object {
    [PSCustomObject]@{
        Id = $_.Id
        Name = $_.ProcessName
        CPU = $(if ($_.CPU) { [math]::Round($_.CPU,2) } else { 0 })
        WorkingSetMB = [math]::Round($_.WorkingSet/1MB,2)
        Threads = $_.Threads.Count
    }
}

$disks = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object {
    [PSCustomObject]@{
        DeviceID = $_.DeviceID
        SizeGB = $(if ($_.Size) { [math]::Round($_.Size/1GB,2) } else { 0 })
        FreeGB = $(if ($_.FreeSpace) { [math]::Round($_.FreeSpace/1GB,2) } else { 0 })
    }
}

$result = [PSCustomObject]@{
    Timestamp = (Get-Date).ToString("o")
    CPU = $cpu
    Memory = $mem
    TopProcesses = $processes
    Disks = $disks
}

$result | ConvertTo-Json -Depth 5
