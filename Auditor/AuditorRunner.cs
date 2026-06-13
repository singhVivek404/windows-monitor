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
        /// <summary>
        /// Loads collected JSON files from <paramref name="dataDir"/>, runs the
        /// health analyzer, and writes a consolidated report.json to
        /// <paramref name="reportsDir"/> (auto-discovered when null).
        /// Returns 0 on success, non-zero on failure.
        /// </summary>
        public static int Run(
            string          dataDir,
            string?         reportsDir = null,
            Action<string>? log        = null)
        {
            try
            {
                if (!Directory.Exists(dataDir))
                {
                    log?.Invoke($"Data directory not found: {dataDir}");
                    return 1;
                }

                log?.Invoke("Loading collected JSON files...");
                var loader = new JsonLoader(dataDir, log);   // log wired in to surface parse errors

                var machine   = loader.LoadSingle<MachineInfo>("machine");
                var processes = loader.LoadMany<ProcessInfo>("processes").ToList();
                var services  = loader.LoadMany<ServiceInfo>("services").ToList();
                var startup   = loader.LoadMany<StartupProgram>("startup").ToList();
                var disks     = loader.LoadMany<DiskInfo>("disk").ToList();
                var software  = loader.LoadMany<SoftwareInfo>("software").ToList();
                var network   = loader.LoadMany<NetworkConnection>("network").ToList();
                var devEnv    = loader.LoadSingle<DevEnvironmentInfo>("devenv");   // NEW

                log?.Invoke($"Loaded: machine={machine?.ComputerName ?? "(unknown)"}, " +
                            $"processes={processes.Count}, devEnv={devEnv != null}");

                log?.Invoke("Running health analysis...");
                var analyzer = new Services.HealthAnalyzer();
                var analysis = analyzer.Analyze(machine, processes, services, startup, disks, devEnv);

                var report = new
                {
                    CollectedAt    = DateTime.UtcNow,
                    Machine        = machine,
                    Processes      = processes,
                    Services       = services,
                    Startup        = startup,
                    Disks          = disks,
                    Software       = software,
                    Network        = network,
                    DevEnvironment = devEnv,   // NEW: included in report for UI dev-tab
                    Analysis       = analysis
                };

                var outDir     = reportsDir ?? FindReportsDir();
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                var reportPath = Path.GetFullPath(Path.Combine(outDir, "report.json"));
                var opts       = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, opts));
                log?.Invoke($"Report written: {reportPath}");
                return 0;
            }
            catch (Exception ex)
            {
                log?.Invoke("Analyzer error: " + ex.ToString());
                return 2;
            }
        }

        // ───────────────────────────────────────────────────────────────────────
        private static string FindReportsDir()
        {
            var cwd = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(cwd, "Reports"),
                Path.Combine(cwd, "..", "Reports"),
                Path.Combine(AppContext.BaseDirectory, "Reports"),
                Path.Combine(AppContext.BaseDirectory, "..", "Reports")
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
