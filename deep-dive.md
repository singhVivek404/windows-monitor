# Workstation Auditor: Deep Dive & Technical Internals

Welcome to the Deep Dive. This document is designed to take you under the hood of the Workstation Auditor. As developers, we don't just want to know *what* the code does; we need to know *why* it was written that way. We'll explore the specific design patterns, concurrency challenges, and automation strategies used in this project.

Grab a coffee, and let's get technical.

---

## 1. Technical Concepts & Challenges

Before we look at the specific code, you need to understand three core technical concepts that shaped this project.

### Concept A: The Race Condition & Atomic Writes
**The Problem:** Our PowerShell scripts write data to JSON files, and our C# application uses a `FileSystemWatcher` to detect when those files are created so it can parse them. However, writing a file to disk isn't instantaneous. If C# tries to read the file while PowerShell is still halfway through writing it, C# will crash with a "JSON Parsing Error" or an "Access Denied" error. This is a classic **Race Condition**.
**The Solution:** Atomic Writes. In our PowerShell scripts, we don't write directly to `report.json`. Instead, we write all the data to a temporary file: `report.tmp`. Once the write is 100% complete, we rename the file from `.tmp` to `.json`. 
*Why does this work?* In Windows, renaming a file on the same drive is an **Atomic Operation**. It happens instantly at the file-system level. The C# `FileSystemWatcher` is configured to only look for `.json` files. Therefore, C# is completely blind to the file until it is perfectly, completely ready.

### Concept B: Embedded Resources & Portability
**The Problem:** The tool relies on a bunch of `.ps1` scripts. If we just zip the executable and the scripts together, the user has to extract them to a folder, and if they accidentally move the executable away from the scripts, the program breaks.
**The Solution:** We embed the PowerShell scripts directly inside the C# executable at compile time (using `<EmbeddedResource>`). When the application starts, it silently extracts these scripts to `%LOCALAPPDATA%\WindowsMonitor`. 
*Why does this work?* It guarantees that the executable is 100% portable. The user gets a single `.exe` file. They can run it from their Desktop, from a USB drive, anywhere—and the application handles setting up its own dependencies behind the scenes.

### Concept C: Debouncing the UI Thread
**The Problem:** When the PowerShell orchestrator runs, it might generate 8 JSON files in a fraction of a second. If the UI responds to every single file creation event and attempts to redraw the entire screen 8 times a second, the application will freeze and lock up the UI Thread.
**The Solution:** Debouncing. We introduce a deliberate delay using `Task.Delay` and a `CancellationToken`. When the first file arrives, we start a countdown timer (e.g., 500 milliseconds). If another file arrives before the timer hits zero, we reset the timer. We only redraw the UI when the system has been "quiet" long enough for the timer to reach zero. This ensures a smooth, single update to the UI.

---

## 2. PowerShell Scripts (`scripts/`)

The PowerShell scripts are the "sensors" of our application. They interact directly with the Windows Management Instrumentation (WMI) and the Registry.

* **`AuditCollector.ps1` (The Orchestrator):** This is the master script. It creates the `Reports` directory and then executes all the individual collectors below. It's responsible for the overall execution flow on the PowerShell side.
* **`Collector-DevEnv.ps1`:** Highly specific to developers. It checks the system `PATH` for tools like Git, Node.js, and Python, verifies their versions, and checks environment variables.
* **`Collector-Disk.ps1`:** Uses `Get-CimInstance Win32_LogicalDisk` to find physical drives and calculate free space percentages.
* **`Collector-Machine.ps1`:** Gathers the base hardware and OS specs (CPU, RAM, OS Version).
* **`Collector-Network.ps1`:** Queries IP addresses, DNS configurations, and active adapters.
* **`Collector-Processes.ps1` & `Collector-Services.ps1`:** Snapshots what is currently running on the machine, looking for resource hogs or stopped critical services.
* **`Collector-Software.ps1`:** Dives into the Windows Registry (both 32-bit and 64-bit hives) to list installed applications.
* **`Collector-Startup.ps1`:** Checks the `Run` keys in the registry to see what applications are configured to start automatically when the user logs in.

*Notice the pattern?* Every script follows a strict rule: Gather data -> Convert to PSCustomObject -> Export to JSON (using the Atomic Write pattern).

---

## 3. C# .NET Backend (`Auditor/`)

This is a Class Library (`.dll`), meaning it contains no visual elements. It is purely focused on data and business logic.

* **The Models:** We have C# classes that mirror the structure of the JSON files. We use `System.Text.Json` to deserialize the JSON text into these strongly-typed C# objects.
* **`AuditorRunner.cs`:** This class acts as the bridge to PowerShell. It sets up the `ProcessStartInfo`, configures the `ExecutionPolicy` to Bypass (so our scripts can run even on restricted machines), and captures the Standard Output and Error streams from the hidden PowerShell window.
* **`HealthAnalyzer.cs`:** This is where the "Auditing" actually happens. It takes the parsed data and applies business rules. For example: *If Disk Free Space < 10%, set status to WARNING.* It generates human-readable recommendations based on the raw data.

---

## 4. C# Frontend (`Auditor.UI/`)

This is the WinForms application that the user interacts with.

* **`MainForm.cs`:** The core window. It registers the `FileSystemWatcher` and holds the debouncing logic we discussed earlier.
* **High-DPI Scaling:** In older Windows apps, developers used absolute coordinates (e.g., "Put the button exactly at pixel 100x, 50y"). If a user has a 4K monitor and sets their Windows Scaling to 150%, absolute coordinates cause text to overlap and clip. 
  * *How we solved it:* We use `TableLayoutPanel` and `FlowLayoutPanel`. These are dynamic containers. They arrange controls relative to each other (like an HTML grid or flexbox). If the text gets bigger, the container automatically stretches to accommodate it, ensuring the application looks perfect on any screen.
* **RichTextBox Controls:** For data-heavy tabs (like installed software or environment variables), we use scrolling RichTextBoxes instead of standard Labels to ensure that massive amounts of text don't break out of the window boundaries.

---

## 5. Automation & CI/CD (`.github/workflows/release.yml`)

We don't want to manually compile and zip the application every time we make a change. We use GitHub Actions to automate this.

### The Pipeline Workflow:
1. **Trigger:** The pipeline is triggered manually via `workflow_dispatch` or automatically when a new release tag is created.
2. **Setup:** It spins up a fresh `windows-latest` virtual machine in the cloud.
3. **Build:** It runs `dotnet publish` to compile the C# code in `Release` mode, creating our optimized, single-file executable.
4. **Installer Creation (Inno Setup):** We don't just ship the `.exe`. We use a tool called Inno Setup. The pipeline takes our compiled `.exe` and packages it into a professional `WorkstationAuditorSetup.exe` installer.
   * *Why an installer?* The Inno Setup script does more than just copy files. It can automatically execute administrative commands during installation, like permanently setting the PowerShell execution policy to `RemoteSigned`, fixing permission issues before the user even runs the app.
5. **Release:** Finally, the `softprops/action-gh-release` step takes the generated setup file and attaches it to a GitHub Release, making it instantly available for users to download.

---

## Summary

You now have a deep understanding of the Workstation Auditor. You understand the "Why" behind atomic writes, debounced UI threads, dynamic DPI layouts, and embedded resource extraction. 

If you need to extend this project, follow the patterns established here: keep your scripts modular, use atomic JSON handoffs, keep business logic in the `Auditor` library, and rely on the pipeline for consistent deployments.
