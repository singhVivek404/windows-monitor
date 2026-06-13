# Workstation Auditor — Technical Deep Dive

This document details the code implementations, scripting techniques, C# backend architecture, WinForms UI mechanics, and build automation pipelines in the Workstation Auditor project.

---

## 1. PowerShell Diagnostics Collectors

Data collection is written in PowerShell. The script suite queries WMI/CIM, checks registry hives, scans directory profiles, and runs external CLI tools.

### `AuditCollector.ps1` (Orchestration Runner)
*   **Purpose:** Coordinates running the other collector scripts in parallel.
*   **Mechanism:** Resolves the script directory using `$PSScriptRoot`. It takes a parameter `-OutputDir` (defaulting to `.\Data`) and runs each `Collector-*.ps1` script as a separate PowerShell background job or execution process. This prevents slow individual scripts (like registry scanners) from blocking fast ones.

### `Collector-Machine.ps1` (System Capacity Telemetry)
*   **Query Method:** Sourced via WMI/CIM cmdlets (`Get-CimInstance`).
*   **Metrics Sourced:**
    *   **RAM:** `Win32_ComputerSystem.TotalPhysicalMemory` (total capacity) and `Win32_OperatingSystem.FreePhysicalMemory` (free physical RAM). Sourcing the free RAM directly from the OS provides kernel-level accuracy (which accounts for file system cache, kernel stacks, and device drivers), unlike summing process working sets.
    *   **CPU:** `Win32_Processor` details (name, core count, logical threads).
    *   **Uptime:** `Win32_OperatingSystem.LastBootUpTime` is captured to compute total uptime.

### `Collector-Processes.ps1` (Process Memory Map)
*   **Mechanism:** Calls `Get-Process | Sort-Object -Property WorkingSet -Descending | Select-Object -First 50`.
*   **Metrics Sourced:** Processes are mapped to their ProcessName, PID, CPU Usage, and Working Set (converted to MB).

### `Collector-Disk.ps1` (Physical Space Metrics)
*   **Mechanism:** Queries `Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3"` (DriveType 3 represents fixed local disks, preventing mapped network drives or removable USBs from blocking queries).
*   **Metrics Sourced:** Letter drive identifiers, total sizes, free space, and used percentages.

### `Collector-Network.ps1` (O(1) Socket Mapping)
*   **Optimization:** Maps established/listening connections to process names. 
*   **Code Implementation:**
    ```powershell
    # Builds a process-to-name dictionary ONCE (O(N) lookup complexity)
    $procMap = @{}
    Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
        if (-not $procMap.ContainsKey($_.Id)) { $procMap[$_.Id] = $_.ProcessName }
    }
    
    # Resolves connection stats
    Get-NetTCPConnection -ErrorAction SilentlyContinue | ForEach-Object {
        $procName = $procMap[[int]$_.OwningProcess] # O(1) dictionary key fetch
        # Adds to outputs...
    }
    ```
*   **Alternative Fallback:** If `Get-NetTCPConnection` is not available (older OS versions), it parses the command output of `netstat -ano` using regular expressions, looking up the resolved PIDs in the same pre-built hash dictionary.

### `Collector-DevEnv.ps1` (Developer Environment Auditor)
The dev environment script compiles developer-specific resource telemetry:
1.  **WSL2 Disk Check:** Executes `wsl.exe --list --verbose` to find distributions, then queries the registry path `HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss` to resolve the absolute path of the `ext4.vhdx` virtual hard disks. It measures their sizes on disk to audit virtual disk growth.
2.  **Docker Metrics:** Executes `docker info` and parses JSON output from `docker container ls -a --format json` and `docker image ls --format json` to count pulled images and containers.
3.  **Package Cache Audit:** Scans standard development package storage directories:
    *   `NuGet`: `%UserProfile%\.nuget\packages`
    *   `npm`: `%AppData%\npm-cache`
    *   `pip`: `%LocalAppData%\pip\Cache`
    *   `Cargo`: `%UserProfile%\.cargo\registry\cache`
    *   `pnpm`: `%LocalAppData%\pnpm\store`
    *   `Gradle`: `%UserProfile%\.gradle\caches`
    Returns directory sizes in GB by summing file capacities.
4.  **PATH Variables & Long Paths:** Counts characters in the `$env:PATH` string. It also checks the Windows registry key `HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled` to warn when the 260-character path limit is still active.

---

## 2. C# Core Backend Library (`Auditor`)

The backend library performs model bindings, JSON schema validation, and health score logic.

### JSON Loader Engine (`JsonLoader.cs`)
*   **Functionality:** Exposes `LoadSingle<T>` and `LoadMany<T>` helpers.
*   **Error Handling:** It encapsulates JSON deserialization within `try-catch` blocks catching `JsonException`. Rather than throwing silently or crashing the application, it writes the parse error details to the logging delegate (`_log`) and returns the `default` value or an empty enumeration, allowing the remaining collectors to display.

### Health Analysis Engine (`HealthAnalyzer.cs`)
*   **Rules Engine:** It evaluates snapshots against critical thresholds:
    *   *Disk Critical:* Used space >= 95% (-20 pts).
    *   *Disk Warning:* Used space >= 80% (-10 pts).
    *   *RAM Critical:* Sourced via kernel free RAM >= 90% (-20 pts).
    *   *RAM Warning:* Sourced via kernel free RAM >= 70% (-10 pts).
    *   *Startup Programs:* Count >= 20 (-15 pts); count > 10 (-5 pts).
    *   *Long Uptime:* Windows boot time > 14 days (-5 pts).
    *   *WSL2 Bloat:* Combined WSL2 vhdx files > 50 GB (Medium warning, -5 pts).
    *   *Cache Bloat:* Combined package caches > 10 GB (Low warning, -3 pts).
    *   *Orphaned Build Processes:* Orphaned tools counts >= 3 (-3 pts).
*   **Recommendation Matrix:** Maps warning categories to specific strings containing recommended actions. These strings are parsed by the UI to register automated click triggers (e.g. launching `taskmgr` or `cleanmgr.exe`).

### Analytical Orchestrator (`AuditorRunner.cs`)
*   **Method:** Calls `JsonLoader` to build collection objects, passes them to `HealthAnalyzer`, and formats a structured payload containing the machine info, logs, analysis warnings, recommendations, and dev environment findings.
*   **Atomic Write Pattern:** To prevent read/write conflicts with the UI's filesystem monitoring thread, it writes the report file atomically:
    ```csharp
    var tempPath = reportPath + ".tmp";
    File.WriteAllText(tempPath, JsonSerializer.Serialize(report, opts));
    File.Move(tempPath, reportPath, overwrite: true); // Atomic rename/replace operation
    ```

---

## 3. C# WinForms Frontend Dashboard (`Auditor.UI`)

The UI dashboard is written using C# Windows Forms.

### DPI-Aware Responsive Layouts
To ensure layout components fit on various resolution ratios and high-DPI scaling configurations:
1.  **Grid Alignments:** Rather than manual coordinates, the top panel header and overview panels are housed inside a `TableLayoutPanel` using relative percentage widths (e.g., 60% left column / 40% right column). Row heights are set to `SizeType.AutoSize` or `SizeType.Percent` to dynamically space elements.
2.  **Dynamic Card Auto-Sizing:** The flow layout panel `flpRecs` (Recommendations list) exposes a `Resize` handler that updates card widths when the window scales:
    ```csharp
    flpRecs.Resize += (s, e) => {
        int w = flpRecs.ClientSize.Width - 20;
        foreach (Control c in flpRecs.Controls) {
            if (c is Panel card) {
                card.Width = w;
                foreach (Control sub in card.Controls) {
                    if (sub is Label) sub.Width = card.Width - 108;
                    else if (sub is Button) sub.Left = card.Width - 92;
                }
            }
        }
    };
    ```
3.  **Button wrapping:** The bottom button panel `pnlButtons` has `WrapContents = true` and `AutoSize = true` with `AutoSizeMode.GrowAndShrink`. If the user shrinks the dashboard width, the buttons wrap onto new lines, automatically growing the height of the button bar.

### Embedded Resources Extraction Pipeline
To ensure the published single-file EXE (`Auditor.UI.exe`) is self-contained:
*   The project file [Auditor.UI.csproj](file:///d:/windows-monitor/Auditor.UI/Auditor.UI.csproj) links the root-level scripts as embedded resources:
    ```xml
    <EmbeddedResource Include="..\*.ps1" Link="Resources\%(Filename)%(Extension)" />
    ```
*   On startup, `MainForm` runs `EnsureCollectorsExtracted()`. This method calls `Assembly.GetManifestResourceNames()`, identifies resources ending in `.ps1`, and writes them to `%LOCALAPPDATA%\WorkstationAuditor\` using `File.Create()` and `stream.CopyTo()`.
*   If `RunFullAuditAsync()` fails to find the scripts in parent directories (production environment), it falls back to `%LOCALAPPDATA%\WorkstationAuditor\AuditCollector.ps1`.

### Multithreaded Execution & Watcher Debouncing
*   **PowerShell Invocations:** Background collection is run on a task thread using `await RunProcessAsync(psi, "Collector", 360_000)` ensuring the UI thread remains completely interactive and does not lock.
*   **FileSystem Watcher:** File changes trigger the `FileSystemWatcher.Changed` event. To prevent parsing a partially written report, the event handler schedules a non-blocking `Task.Delay(350)` for debouncing before calling `RefreshReportAsync()`.
*   **Asynchronous Report Loading:** File loading and retries are run off-thread via `Task.Run()`. Once the text is successfully read, `BeginInvoke` marshals the JSON document back to the main UI thread to update controls.

---

## 4. Build and Release Automation

The project automation handles self-contained builds, installer compiling, and GitHub tag releases.

### Compile & Publish Scripts (`scripts/`)
*   `publish-windows.ps1`: Invokes the dotnet publisher engine targeting Windows x64.
    ```powershell
    dotnet publish Auditor.UI/Auditor.UI.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish
    ```
    It then copies all raw `.ps1` collector scripts into the `publish/` output folder (where the single-file EXE expects them).
*   `build-installer.ps1`: Locates the Inno Setup compiler executable (`ISCC.exe`) in standard paths (or environment overrides) and compiles the setup configuration script.

### Inno Setup Installer (`installer/setup.iss`)
*   Packages the self-contained executable and PowerShell scripts into a compressed setup installer `WorkstationAuditorSetup.exe` inside the `dist/` directory.
*   **Registry Modification:** Writes to the registry hive:
    `Root: HKCU; Subkey: "Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell"; ValueName: "ExecutionPolicy"; ValueData: "RemoteSigned";`
    This ensures PowerShell script collectors have permission to execute on the end user's machine without requiring manual console configuration.

### GitHub Actions Pipeline (`release.yml`)
Located at `.github/workflows/release.yml`, it automates releases on GitHub:
1.  **Triggers:** Runs on manual execution (`workflow_dispatch`) or whenever a version tag like `v*` is pushed.
2.  **Workflow Steps:**
    *   Spawns a standard Windows runner (`runs-on: windows-latest`).
    *   Installs **.NET 10 SDK**.
    *   Invokes `publish-windows.ps1` to produce the single-file publish artifacts.
    *   Runs the pre-installed Inno Setup compiler (`ISCC.exe`) against `installer/setup.iss` to package the setup installer.
    *   Uses the `softprops/action-gh-release@v2` action to create a GitHub Release and upload `dist/WorkstationAuditorSetup.exe` as a release asset.
3.  **Ternary Fallback Logic:** 
    `tag_name: ${{ startsWith(github.ref, 'refs/tags/') && github.ref_name || 'latest' }}`
    Ensures manual pipeline runs fallback to publishing a rolling prerelease release under the tag **`latest`**, while version tag pushes publish to matching tag version paths.
