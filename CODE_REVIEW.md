# Code Review: Workstation Auditor

Date: 2026-06-14

This document is a focused review of the current `windows-monitor` / Workstation Auditor codebase. It summarizes high-impact bugs, reliability and UX issues, security/privacy concerns, and prioritized recommendations for short-term fixes and medium-term improvements.

Summary
-------
- Status: functional prototypes exist (collectors produce JSON, analyzer writes `Reports/report.json`, UI loads and displays logs) but the product is not yet reliable, discoverable, or user-friendly for end users.
- High-level rating: Needs Improvement — several correctness and UX bugs should be addressed before a consumer release.

Major Issues (High Priority)
----------------------------

1. Distribution / Pathing Fragility
   - Problem: The UI and scripts locate resources by walking parent folders from `AppDomain.CurrentDomain.BaseDirectory` (e.g. `FindFileInParents("AuditCollector.ps1")`). If the single-file EXE is copied outside the repo, collectors won't be found and Setup fails with: "Cannot find AuditCollector.ps1 in parent folders." See: [Auditor.UI/MainForm.cs](Auditor.UI/MainForm.cs).
   - Impact: Published EXE is not self-contained; double-click behavior is broken for end users.

2. Incorrect RAM usage calculation (Analyzer correctness)
   - Problem: `HealthAnalyzer` sums per-process working sets to estimate total memory usage. Summing process working sets double-counts shared pages and omits OS/FS cache and kernel memory. This yields inaccurate recommendations. See: [Auditor/Services/HealthAnalyzer.cs](Auditor/Services/HealthAnalyzer.cs).
   - Fix: Use OS-level metrics (WMI/CIM `Win32_OperatingSystem` TotalVisibleMemorySize / FreePhysicalMemory or `Microsoft.Management.Infrastructure`) to compute system memory utilization.

3. Collectors: PowerShell overhead and inefficiencies
   - Problem A: Relying on many PowerShell scripts means heavy startup cost and fragile execution policy dependencies. `AuditCollector.ps1` orchestration is brittle when run from different folders. See: [AuditCollector.ps1](AuditCollector.ps1).
   - Problem B: `Collector-Network.ps1` resolves a process name per connection using `Get-Process` inside a loop. This is O(N) expensive for each socket and can take minutes on busy machines.
   - Fix: Batch process lookups (collect all unique `OwningProcess` ids, map to names once), or port high-value collectors to native C# using `System.Management`/CIM.

4. UI blocks and fragile file-watcher handling
   - Problem: `FileSystemWatcher` events call `RefreshReport()` which performs synchronous retries with `Thread.Sleep(...)` while parsing. This may run on the UI thread and freeze the UI during retries. See: [Auditor.UI/MainForm.cs](Auditor.UI/MainForm.cs).
   - Fix: Debounce filesystem events and parse reports on a background thread (Task.Run) then marshal updates to the UI thread. Avoid Thread.Sleep on the UI thread.

5. Silent deserialization failures
   - Problem: `JsonLoader` swallows exceptions and returns empty sequences on errors. This hides critical errors and produces empty UIs rather than actionable error messages. See: [Auditor/Services/JsonLoader.cs](Auditor/Services/JsonLoader.cs).
   - Fix: Surface parse errors to `Reports/ui.log` and show a clear error in the UI (with guidance to open the log). Consider returning a Result<T> object with error details instead of swallowing exceptions.

6. Build / Packaging issues
   - Problem: `Auditor` is built as an executable (OutputType=Exe) but used as a library by `Auditor.UI`. This is confusing and can introduce duplicate entrypoints. See: [Auditor/Auditor.csproj](Auditor/Auditor.csproj).
   - Fix: Change to `<OutputType>Library</OutputType>` and expose an explicit API surface for `AuditorRunner`.

Other Notable Issues
--------------------
- File write/read races: `report.json` is written by analyzer and read by UI. Use atomic write patterns (write to a temp file then replace/rename) and open files with appropriate FileShare flags when reading.
- UI layout uses manual absolute positioning for many controls (Top/Left). This causes poor DPI scaling and clipped controls on high-DPI displays. Move to responsive layout controls and make fonts DPI-aware.
- Kill/action buttons: dangerous if used without confirmation. Keep confirmations for destructive actions and limit by UAC/elevation requirements.
- Logging: `Reports/ui.log` is useful but logs are very verbose and currently the UI is log-first. End users want an actionable summary first.

Repro Steps (observed failures)
------------------------------
1. Double-click published EXE outside the repository → Setup fails with "Cannot find AuditCollector.ps1". (Distribution/pathing issue.)
2. Run `Setup` → `Run Audit` from UI → UI shows logs but sometimes freezes briefly while the report is written. (UI blocking on report parse.)
3. Analyzer `HealthScore` fluctuates or gives odd recommendations when system memory is heavily used due to wrong memory calculation. (Analyzer correctness.)

Short-Term Prioritized Fixes (Quick Wins)
----------------------------------------
1. Pathing & AppData fallback (top priority)
   - Store runtime `Data/` and `Reports/` under `%LOCALAPPDATA%\WorkstationAuditor` when `AuditCollector.ps1` is not found. Update `FindRepositoryRoot()` fallback logic and `GetReportsDirectory()`/`GetDataDirectory()` in `MainForm.cs`.
   - Ensure `scripts/publish-windows.ps1` documents whether the EXE expects to be in a repo; update to copy initial collectors into AppData on first run or bundle a minimal collector to AppData.

2. Make report writes atomic and resilient
   - Analyzer: write to a temp file and rename/replace the final report file. E.g. write to `report.json.tmp` then move/replace to `report.json`. This avoids partial reads.
   - UI: open with `FileShare.ReadWrite` or use a retry with exponential backoff running off the UI thread.

3. Fix RefreshReport threading and watcher debounce
   - Debounce FileSystemWatcher events (200–500ms) and parse JSON in `Task.Run(...)`; marshal UI changes via `BeginInvoke`. Remove `Thread.Sleep` from UI path.

4. Fix memory calculation (HealthAnalyzer)
   - Replace process-sum heuristic with WMI-based system totals:

```csharp
using System.Management; // Windows-only
var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
foreach (ManagementObject mo in searcher.Get())
{
    ulong totalKb = (ulong)mo["TotalVisibleMemorySize"];
    ulong freeKb = (ulong)mo["FreePhysicalMemory"];
    double usedPercent = ((double)(totalKb - freeKb) / totalKb) * 100.0;
}
```

5. Stop swallowing JSON deserialization errors
   - Update `JsonLoader` to either (a) return a Result/Option with error details, or (b) log full exception details and throw up to the caller so the UI can surface it.

Medium-Term / Architectural Recommendations
-----------------------------------------
- Convert `Auditor` to a clean class library and extract shared models into a single `WorkstationAuditor.Models` project.
- Port high-value collectors to C# for speed, reliability, and stronger error handling; keep complex or very OS-specific logic in small PowerShell helpers only when necessary.
- Move to a background tray service (or single-process background worker) with a small dashboard client connecting via local HTTP/WebSocket. This enables real-time updates and reduces heavy script invocations.
- Consider modernizing the UI to WPF / WinUI3 or a web-based frontend (Tauri/React) for better visuals and cross-device compatibility.
- Adopt an MVP/MVVM pattern for the UI so business logic is separated from rendering; this simplifies testing.

Performance Optimizations
-------------------------
- Network collector: build a dictionary of OwningProcess IDs to name in one pass instead of calling `Get-Process` per connection.
- Disk and software collectors: avoid scanning the whole filesystem synchronously on UI thread; use background tasks and progress reporting.

Security & Privacy
------------------
- Always ask for explicit confirmation before performing destructive actions (kill process, delete caches, shrink VHD). Keep an audit trail in `Reports/ui.log`.
- Do not request elevation by default. If elevation is required for a specific action, escalate only for that action and explain why.
- If telemetry or remote report upload is added, make it opt-in and document exactly what metadata is sent.

Testing & CI
------------
- Add unit tests for `HealthAnalyzer` scoring rules and `JsonLoader` parsing logic using sample JSON payloads.
- Add integration tests that run collectors against recorded sample outputs and assert analyzer outputs.
- Add GitHub Actions that build `Auditor` and `Auditor.UI`, run the analyzer tests, and optionally produce a signed single-file publish artifact.

Suggested Acceptance Criteria (before shipping a consumer EXE)
----------------------------------------------------------------
1. Double-clicking the published EXE on a new Windows account produces a working UI that can run an audit and display the `HealthScore` and recommendations.
2. `Reports/report.json` is written atomically and UI refreshes without freezing.
3. `HealthAnalyzer` uses OS-level memory metrics; recommendations about RAM/disk are accurate and actionable.
4. Collectors complete in reasonable time on a typical developer workstation (<= 30s for full audit) or they provide progress/probable-time estimates.
5. The UI is usable at 125%/150% DPI and the score is visually prominent; logs are accessible but not the default UX.

Next Steps (recommended order)
-----------------------------
1. Implement AppData fallback and atomic report writes (1–2 days).
2. Fix UI watcher/refresh to be asynchronous and debounced (1 day).
3. Fix memory calculation and add unit tests (1 day).
4. Optimize `Collector-Network.ps1` or port it to C# (1–3 days).
5. Convert `Auditor` to a library and refactor `Auditor.UI` to an MVP structure (3–7 days).

If you want, I can begin by implementing item 1 (AppData fallback + atomic writes) and item 2 (non-blocking report refresh) in the repo. Tell me which I should pick first and I will open a branch, implement the changes, run the app locally, and attach the patch.

---
Generated by code review on 2026-06-14
