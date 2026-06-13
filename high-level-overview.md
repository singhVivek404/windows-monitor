# Workstation Auditor: High-Level Overview

Welcome to the Workstation Auditor project! As a developer or system administrator, understanding *how* a system works at a high level is just as important as knowing how to code it. Think of this document as your map to the project. We won't get bogged down in every line of code yet—instead, we're going to look at the big picture. 

Imagine you need to diagnose a problem on a developer's machine. You need data (telemetry), you need to analyze that data (backend), and you need to show the results to the user clearly (frontend). This project does exactly that.

Let's break down the architecture, design, orchestration, and workflow, step-by-step.

---

## 1. Architecture: The Decoupled Approach

When designing a system that collects system-level data and displays it in a UI, a common mistake is to try and do everything in one place. If the UI thread is busy querying the Windows Registry or scanning the disk, the whole application freezes.

**Concept: Decoupling**
To avoid freezing and to keep things clean, we use a **Decoupled Architecture**. We separate the "Data Collection" from the "Data Analysis and Display". 

Here is our pipeline:
1. **PowerShell (Telemetry Collection):** PowerShell is incredibly powerful for querying Windows OS details. We use it to gather raw data.
2. **JSON Files (The Bridge):** The PowerShell scripts save the gathered data into standard JSON files. JSON acts as a universal language between our scripts and our application.
3. **C# .NET Backend (Analysis):** The C# application reads the JSON files, analyzes the data against certain health rules, and prepares the results.
4. **WinForms UI (Frontend):** Finally, the C# UI layer takes the analyzed results and displays them beautifully to the user.

By communicating through JSON files, the PowerShell scripts and the C# application never have to wait on each other directly.

---

## 2. Orchestration and Workflow: How It Runs

How does everything start? What happens when a user double-clicks the application? 

**Concept: Self-Contained Deployment**
We want the user to have a seamless experience. They shouldn't have to worry about putting scripts in the right folders. The application is compiled as a **Single-File EXE**. 

Here is the exact workflow:

### Step A: Bootstrapping and Extraction
1. **Launch:** The user runs the `Auditor.UI` executable.
2. **Extraction:** The C# application has all the PowerShell scripts bundled inside it as *Embedded Resources*. When it starts, it automatically extracts these scripts to the user's `%LOCALAPPDATA%\WindowsMonitor` folder. This ensures the scripts are always available and don't clutter the user's current directory.

### Step B: Execution and Telemetry
3. **Execution:** The C# application orchestrates the PowerShell execution by running the main `AuditCollector.ps1` script in the background.
4. **Collection:** The `AuditCollector.ps1` script calls various specialized collectors (e.g., Disk, Network, DevEnv) which gather data and write it out to JSON reports in a `Reports/` directory.

### Step C: Reactivity and Analysis
5. **Debouncing & File Watching:** The C# backend uses a `FileSystemWatcher` to monitor the `Reports/` folder. Every time a PowerShell script finishes writing a JSON file, the C# backend is notified.
   - *Concept: Debouncing.* If 10 files are written in one second, we don't want to refresh the UI 10 times. We use a technique called "debouncing" (via `Task.Delay`) to wait until the flurry of updates settles, and then we refresh the UI just once.
6. **Analysis:** The `HealthAnalyzer` component kicks in, evaluating the data (e.g., "Is the disk almost full?", "Are there unauthorized startup apps?").

### Step D: Presentation
7. **UI Update:** The WinForms UI reads the analysis and renders the data onto the screen using dynamic layouts.

---

## 3. Components and Their Purpose

Let's look at the major building blocks of the project:

### 1. The PowerShell Collectors (`Collector-*.ps1`)
**Purpose:** These are the workers. Each script has one specific job (e.g., `Collector-Disk.ps1` only looks at drives, `Collector-Network.ps1` only looks at IP addresses and adapters). 
**Why?** *Separation of Concerns*. If we want to add a new feature to check Docker containers, we just add a new `Collector-Docker.ps1` script instead of modifying a massive, thousands-of-lines-long script.

### 2. The Orchestrator Script (`AuditCollector.ps1`)
**Purpose:** The manager of the collectors. It ensures all the individual `Collector-*.ps1` scripts run smoothly and standardizes where they output their JSON files.

### 3. The Auditor Class Library (`Auditor/`)
**Purpose:** This is the brain of the C# application. It handles parsing the JSON files, analyzing the health status (the `HealthAnalyzer`), and managing file paths. 
**Why a Class Library?** We separated this from the UI project. This means if we ever want to build a web interface or a command-line version of this tool in the future, we can reuse this exact library without dragging along any WinForms UI code.

### 4. The UI Frontend (`Auditor.UI/`)
**Purpose:** The face of the application. It provides the visual dashboard for the user. It handles the `FileSystemWatcher`, triggers the PowerShell execution, and uses smart layout techniques (like `TableLayoutPanel`) to ensure it looks good on any monitor size.

### 5. Deployment Automation (`.github/workflows` & Inno Setup)
**Purpose:** To make the application easy to distribute. The GitHub Actions pipeline automatically compiles the C# code, bundles the single-file executable, and runs the Inno Setup script to create a professional `.exe` installer. It handles the messy parts of deployment so the developers don't have to.

---

## Summary for the Student

At its core, this project is a **pipeline**: `Data Collection -> Data Storage (JSON) -> Data Analysis -> Visual Display`. 

We prioritize making the tool completely self-contained for the end-user while keeping the codebase highly modular (decoupled) for us developers. 

In the `deep-dive.md` document, we will look under the hood and examine the specific technical challenges we solved, such as Race Conditions, High-DPI Scaling, and background threading.
