# Workstation Auditor — High-Level Overview

This document provides a high-level conceptual overview of the **Developer Workstation Auditor** project, its architecture, design principles, orchestration, and component responsibilities.

---

## 1. Project Overview & System Design

The Workstation Auditor is a lightweight developer-focused system diagnostics application designed specifically for Windows. Unlike generic system utilities, it targets developer-centric resource hogs and environmental issues: virtual disk accumulation in WSL2, background compiler process leaks, unchecked package cache directories, Docker resources, and Windows registry limits (like long paths).

The system architecture is strictly **decoupled and modular**, dividing the execution into four stages:

```mermaid
graph TD
    subgraph Data Gathering (PowerShell)
        A[AuditCollector.ps1] -->|Triggers in Parallel| B[Collector-*.ps1 Scripts]
        B -->|Write Raw Telemetry| C[Data/*.json State Files]
    end

    subgraph Analytical Engine (C# Library)
        C -->|Load & Parse| D[JsonLoader]
        D -->|Evaluate Thresholds| E[HealthAnalyzer]
        E -->|Atomically Write| F[Reports/report.json]
    end

    subgraph User Interface (WinForms)
        F -->|FileSystemWatcher Trigger| G[MainForm Dashboard]
        G -->|Action Trigger| H[System Actions / Process Kill]
        H -->|Rerun diagnostics| A
    end
```

### Architectural Design Principles

1.  **Decoupled Collection and Analysis:** Data collection is performed by lightweight PowerShell scripts that run asynchronously. The analysis engine and the UI consume these text files, ensuring that data gathering can be run independently of the UI (e.g., in a CI environment or via Task Scheduler).
2.  **Stateless C# Analyzer:** The C# backend is stateless. It reads JSON files representing the current snapshot of the machine, applies rule logic, computes a health score, and writes a single report file. This makes testing business rules simple.
3.  **Event-Driven UI:** The WinForms dashboard utilizes a `FileSystemWatcher` on the report directory. The UI automatically invalidates its drawings and redraws elements whenever a new `report.json` is completed, ensuring real-time responsiveness without busy-waiting.
4.  **DPI-Aware & Responsive Layouts:** Using native layouts (`TableLayoutPanel` and `FlowLayoutPanel` with text wrapping) instead of static absolute positioning ensures that text elements never overlap, regardless of resolution or system scaling (e.g., 125% or 150%).
5.  **Self-Contained Portability:** The compiler scripts are embedded directly inside the C# binary as embedded resources. When run outside the repository, the app extracts the scripts into the user's writable Local AppData folder (`%LOCALAPPDATA%\WorkstationAuditor`).

---

## 2. Components & Their Purpose

Here is a map of the core files in the project and what each is responsible for:

| Component | Path | Purpose |
|---|---|---|
| **Collector Orchestrator** | `AuditCollector.ps1` | Parallel execution runner that invokes all individual collector scripts and passes parameters (e.g. output directory). |
| **System Info Collector** | `Collector-Machine.ps1` | Queries CIM/WMI to pull core CPU names, exact RAM capacities, OS caption, and Last Boot time. |
| **Processes Collector** | `Collector-Processes.ps1` | Grabs the current process list, measuring memory and CPU footprint, sorted to return the top 50. |
| **Disk Collector** | `Collector-Disk.ps1` | Scans local logical disks to determine partition names, sizes, and free percentages. |
| **Services Collector** | `Collector-Services.ps1` | Lists critical background Windows services, their start statuses, and configurations. |
| **Startup Collector** | `Collector-Startup.ps1` | Queries the registry (HKLM and HKCU) and the local Start Menu folders to audit startup items. |
| **Network Collector** | `Collector-Network.ps1` | Lists established and listening TCP connections, mapping owning process IDs to their process names. |
| **Software Collector** | `Collector-Software.ps1` | Collects installed programs from standard registry uninstall keys (32-bit and 64-bit hives). |
| **Dev Environment Collector** | `Collector-DevEnv.ps1` | Scans developer-specific indicators: WSL2 distributions, Docker image/container states, sizes of npm/NuGet/pip/Cargo caches, PATH variables, and the `LongPathsEnabled` registry key. |
| **C# Models Project** | `Auditor/Models/` | Houses plain C# classes (`DiskInfo`, `MachineInfo`, `ProcessInfo`, `DevEnvironmentInfo`) that mirror the collector JSON schemas. |
| **C# JSON Loader** | `Auditor/Services/JsonLoader.cs` | Deserializes the raw output files from the collector, handling parse errors and directing warnings to the log. |
| **C# Analysis Engine** | `Auditor/Services/HealthAnalyzer.cs` | Runs threshold evaluation rules, deducts health scores, and returns warnings and recommendations. |
| **In-Process Runner** | `Auditor/AuditorRunner.cs` | Orchestrates the loader and analyzer, saving the final `report.json` atomically. |
| **WinForms UI** | `Auditor.UI/MainForm.cs` | A modern dark-themed dashboard displaying warnings, responsive recommendations, running processes, disks (scrollable), network connections, and developer environment info. Exposes custom controls to kill processes, shut down WSL, and prune Docker containers. |
| **Workflow Pipeline** | `.github/workflows/release.yml` | GitHub Actions CI/CD configuration to compile, package with Inno Setup, and release the installer on version tag tags. |
| **Inno Setup Script** | `installer/setup.iss` | Script code that defines the installer UI, file installations, shortcuts, and Registry updates for PowerShell permissions. |
| **Packaging Helpers** | `scripts/` | Shell helper files to compile self-contained binaries (`publish-windows.ps1`) and compile installers (`build-installer.ps1`). |
