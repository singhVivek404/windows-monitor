using System;
using System.Collections.Generic;
using System.Linq;
using WorkstationAuditor.Models;

namespace WorkstationAuditor.Services
{
    public class HealthAnalyzer
    {
        public AnalysisResult Analyze(MachineInfo? machine, IEnumerable<ProcessInfo> processes, IEnumerable<ServiceInfo> services, IEnumerable<StartupProgram> startup, IEnumerable<DiskInfo> disks)
        {
            var warnings = new List<AnalysisWarning>();
            int score = 100;

            var diskList = (disks ?? Enumerable.Empty<DiskInfo>()).ToList();
            if (diskList.Any())
            {
                bool diskCritical = diskList.Any(d => d.UsedPercentage >= 95);
                bool diskWarning = !diskCritical && diskList.Any(d => d.UsedPercentage >= 80);
                if (diskCritical)
                {
                    warnings.Add(new AnalysisWarning("HIGH", $"Disk critical: {string.Join(", ", diskList.Where(d => d.UsedPercentage >= 95).Select(d => d.Drive + " " + d.UsedPercentage + "%"))}"));
                    score -= 20;
                }
                else if (diskWarning)
                {
                    warnings.Add(new AnalysisWarning("MEDIUM", $"Disk warning: {string.Join(", ", diskList.Where(d => d.UsedPercentage >= 80).Select(d => d.Drive + " " + d.UsedPercentage + "%"))}"));
                    score -= 10;
                }
            }

            var procList = (processes ?? Enumerable.Empty<ProcessInfo>()).ToList();
            if (machine != null && machine.TotalMemoryGB > 0)
            {
                double totalProcMb = procList.Sum(p => p.MemoryMb);
                double totalMemMb = machine.TotalMemoryGB * 1024.0;
                double memUsedPercent = totalMemMb > 0 ? (totalProcMb / totalMemMb) * 100.0 : 0.0;
                if (memUsedPercent >= 90)
                {
                    warnings.Add(new AnalysisWarning("HIGH", $"RAM usage high: {memUsedPercent:F1}%"));
                    score -= 20;
                }
                else if (memUsedPercent >= 70)
                {
                    warnings.Add(new AnalysisWarning("MEDIUM", $"RAM usage warning: {memUsedPercent:F1}%"));
                    score -= 10;
                }
            }

            int startupCount = (startup ?? Enumerable.Empty<StartupProgram>()).Count();
            if (startupCount >= 20)
            {
                warnings.Add(new AnalysisWarning("HIGH", $"{startupCount} startup programs detected"));
                score -= 15;
            }
            else if (startupCount > 10)
            {
                warnings.Add(new AnalysisWarning("MEDIUM", $"{startupCount} startup programs detected"));
                score -= 5;
            }

            int chromeCount = procList.Count(p => string.Equals(p.Name, "chrome", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, "chrome.exe", StringComparison.OrdinalIgnoreCase));
            if (chromeCount > 20)
            {
                warnings.Add(new AnalysisWarning("MEDIUM", $"Many Chrome processes detected: {chromeCount}"));
            }

            if (machine?.BootTime != null)
            {
                if (TryParseNetDate(machine.BootTime, out var boot) || DateTime.TryParse(machine.BootTime, out boot))
                {
                    var uptime = DateTime.UtcNow - boot.ToUniversalTime();
                    if (uptime.TotalDays > 14)
                    {
                        warnings.Add(new AnalysisWarning("LOW", $"Long uptime: {uptime.TotalDays:F1} days"));
                        score -= 5;
                    }
                }
            }

            if (score < 0) score = 0;

            var recs = BuildRecommendations(warnings);

            return new AnalysisResult
            {
                HealthScore = score,
                Warnings = warnings,
                Recommendations = recs
            };
        }

        private static bool TryParseNetDate(string s, out DateTime dt)
        {
            dt = DateTime.MinValue;
            if (string.IsNullOrEmpty(s)) return false;
            var start = s.IndexOf('(');
            var end = s.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var num = s.Substring(start + 1, end - start - 1);
                if (long.TryParse(num, out var ms))
                {
                    try
                    {
                        dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                        return true;
                    }
                    catch { return false; }
                }
            }
            return false;
        }

        private List<string> BuildRecommendations(List<AnalysisWarning> warnings)
        {
            var recs = new List<string>();
            foreach (var w in warnings)
            {
                var msg = w.Message ?? string.Empty;
                if (msg.IndexOf("Disk", StringComparison.OrdinalIgnoreCase) >= 0) recs.Add("Free up disk space (Disk Cleanup, remove large files)");
                else if (msg.IndexOf("RAM", StringComparison.OrdinalIgnoreCase) >= 0) recs.Add("Close unused applications or consider adding more RAM");
                else if (msg.IndexOf("startup", StringComparison.OrdinalIgnoreCase) >= 0) recs.Add("Disable unnecessary startup programs (Task Manager > Startup)");
                else if (msg.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0) recs.Add("Close unnecessary Chrome tabs or background processes");
                else if (msg.IndexOf("uptime", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("Long uptime", StringComparison.OrdinalIgnoreCase) >= 0) recs.Add("Reboot the machine to clear long-running state");
            }
            return recs.Distinct().ToList();
        }
    }
}
