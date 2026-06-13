using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using WorkstationAuditor.Services;
using WorkstationAuditor.Models;

namespace WorkstationAuditor
{
    public static class AuditorRunner
    {
        // Run analysis using data in dataDir. Writes report into reportsDir (or auto-locates it).
        // Optional log callback will be called with progress messages (may be invoked on a worker thread).
        public static int Run(string dataDir, string? reportsDir = null, Action<string>? log = null)
        {
            try
            {
                if (!Directory.Exists(dataDir))
                {
                    log?.Invoke($"Data directory not found: {dataDir}");
                    return 1;
                }

                log?.Invoke("Loading collected JSON files...");
                var loader = new JsonLoader(dataDir);

                var machine = loader.LoadSingle<MachineInfo>("machine");
                var processes = loader.LoadMany<ProcessInfo>("processes").ToList();
                var services = loader.LoadMany<ServiceInfo>("services").ToList();
                var startup = loader.LoadMany<StartupProgram>("startup").ToList();
                var disks = loader.LoadMany<DiskInfo>("disk").ToList();
                var software = loader.LoadMany<SoftwareInfo>("software").ToList();
                var network = loader.LoadMany<NetworkConnection>("network").ToList();

                log?.Invoke($"Loaded: machine={machine?.ComputerName ?? "(unknown)"}, processes={processes.Count}");

                log?.Invoke("Analyzing...");
                var analyzer = new Services.HealthAnalyzer();
                var analysis = analyzer.Analyze(machine, processes, services, startup, disks);

                var report = new
                {
                    CollectedAt = DateTime.UtcNow,
                    Machine = machine,
                    Processes = processes,
                    Services = services,
                    Startup = startup,
                    Disks = disks,
                    Software = software,
                    Network = network,
                    Analysis = analysis
                };

                var outDir = reportsDir ?? FindReportsDir();
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                var reportPath = Path.GetFullPath(Path.Combine(outDir, "report.json"));
                var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
                var json = JsonSerializer.Serialize(report, opts);
                File.WriteAllText(reportPath, json);
                log?.Invoke($"Wrote report: {reportPath}");
                return 0;
            }
            catch (Exception ex)
            {
                log?.Invoke("Analyzer error: " + ex.ToString());
                return 2;
            }
        }

        static string FindReportsDir()
        {
            var cwd = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(cwd, "Reports"),
                Path.Combine(cwd, "..", "Reports"),
                Path.Combine(AppContext.BaseDirectory, "..", "Reports"),
                Path.Combine(AppContext.BaseDirectory, "Reports")
            };
            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (Directory.Exists(full)) return full;
            }
            return Path.Combine(cwd, "Reports");
        }
    }
}
