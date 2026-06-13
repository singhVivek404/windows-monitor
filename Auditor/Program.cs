using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using WorkstationAuditor.Services;
using WorkstationAuditor.Models;

namespace WorkstationAuditor
{
    internal class Program
    {
        static int Main(string[] args)
        {
            string dataDir = args.Length > 0 ? args[0] : FindDataDir();
            if (!Directory.Exists(dataDir))
            {
                Console.Error.WriteLine($"Data directory not found: {dataDir}");
                return 1;
            }

            var loader = new JsonLoader(dataDir);

            var machine = loader.LoadSingle<MachineInfo>("machine");
            var processes = loader.LoadMany<ProcessInfo>("processes").ToList();
            var services = loader.LoadMany<ServiceInfo>("services").ToList();
            var startup = loader.LoadMany<StartupProgram>("startup").ToList();
            var disks = loader.LoadMany<DiskInfo>("disk").ToList();
            var software = loader.LoadMany<SoftwareInfo>("software").ToList();
            var network = loader.LoadMany<NetworkConnection>("network").ToList();

            Console.WriteLine($"Machine: {machine?.ComputerName ?? "(unknown)"}");
            Console.WriteLine($"Processes: {processes.Count}, Services: {services.Count}, Startup: {startup.Count}, Disks: {disks.Count}, Software: {software.Count}, Network: {network.Count}");

            var analyzer = new HealthAnalyzer();
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

            var reportsDir = FindReportsDir();
            if (!Directory.Exists(reportsDir)) Directory.CreateDirectory(reportsDir);
            var reportPath = Path.GetFullPath(Path.Combine(reportsDir, "report.json"));
            var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
            var json = JsonSerializer.Serialize(report, opts);
            File.WriteAllText(reportPath, json);
            Console.WriteLine($"Wrote report: {reportPath}");
            return 0;
        }

        static string FindDataDir()
        {
            var cwd = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(cwd, "Data"),
                Path.Combine(cwd, "..", "Data"),
                Path.Combine(cwd, "..", "..", "Data"),
                Path.Combine(AppContext.BaseDirectory, "..", "Data"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "Data")
            };
            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (Directory.Exists(full)) return full;
            }
            return Path.Combine(cwd, "Data");
        }

        static string FindReportsDir()
        {
            var cwd = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(cwd, "Reports"),
                Path.Combine(cwd, "..", "Reports"),
                Path.Combine(cwd, "..", "..", "Reports"),
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
