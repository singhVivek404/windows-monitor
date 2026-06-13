Windows Monitor — Workstation Auditor

This project helps answer "Why is my PC slow?" by collecting machine data (PowerShell),
analyzing it (C# Auditor), and presenting results in a small WinForms dashboard.

**Prerequisites**
- Windows (tested on Windows 10/11)
- .NET SDK 10.0.x (project targets net10.0; tested with 10.0.301)
- PowerShell (Windows PowerShell or PowerShell Core). The collectors use `powershell.exe` by default; the UI will prefer `pwsh` if available.

Quickstart
---------
1) Collect system data (generates `Data/*.json`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AuditCollector.ps1
```

2) Run the analyzer to produce `Reports/report.json`:

```powershell
dotnet run --project Auditor/Auditor.csproj
```

3) Launch the WinForms dashboard:

```powershell
dotnet run --project Auditor.UI/Auditor.UI.csproj
```

Full flow (collect → analyze → view):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AuditCollector.ps1
dotnet run --project Auditor/Auditor.csproj
dotnet run --project Auditor.UI/Auditor.UI.csproj
```

Outputs and logs
- `Data/` — raw collector JSON files (machine-specific; ignored by git)
- `Reports/report.json` — aggregated analysis output (ignored by git)
- `Reports/ui.log` — UI log file

Troubleshooting
- If you encounter duplicate assembly attribute or build errors, delete `bin/` and `obj/` folders and rebuild:

```powershell
Get-ChildItem -Path . -Directory -Recurse -Force | Where-Object { $_.Name -in @('bin','obj') } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
dotnet build Auditor.UI/Auditor.UI.csproj
```

- Prefer `dotnet run --project <path>` to run a specific project reliably.

Development notes
- Project layout:
	- `AuditCollector.ps1` — orchestrates `Collector-*.ps1` scripts and writes `Data/`
	- `Auditor/` — core analyzer (loads JSON, builds `Reports/report.json`)
	- `Auditor.UI/` — WinForms dashboard reading `Reports/report.json`

Contributing
- Open an issue or submit a PR. Keep generated data (`Data/`, `Reports/`) out of commits.

License: MIT (or change to your preferred license)

Create a double-clickable EXE
-----------------------------
To produce a single-file Windows EXE you can publish the UI project as a self-contained application. Run:

```powershell
.\scripts\publish-windows.ps1 -Runtime win-x64 -Configuration Release
```

The published executable will appear in the `publish` folder. Copy the EXE to any machine and double-click it to run the app (it includes the Auditor library since the UI references it). The app will auto-run the initial setup if no `Reports/report.json` exists.