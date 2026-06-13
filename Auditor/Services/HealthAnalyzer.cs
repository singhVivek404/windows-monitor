using System;
using System.Collections.Generic;
using System.Linq;
using WorkstationAuditor.Models;

namespace WorkstationAuditor.Services
{
    public class HealthAnalyzer
    {
        /// <summary>
        /// Analyzes collected data and produces a health score, warnings, recommendations,
        /// and developer-specific findings.
        /// </summary>
        public AnalysisResult Analyze(
            MachineInfo?                  machine,
            IEnumerable<ProcessInfo>      processes,
            IEnumerable<ServiceInfo>      services,
            IEnumerable<StartupProgram>   startup,
            IEnumerable<DiskInfo>         disks,
            DevEnvironmentInfo?           devEnv = null)
        {
            var warnings   = new List<AnalysisWarning>();
            var devFindings = new List<string>();
            int score      = 100;

            // ── Disk space analysis ─────────────────────────────────────────────
            var diskList = (disks ?? Enumerable.Empty<DiskInfo>()).ToList();
            if (diskList.Any())
            {
                bool diskCritical = diskList.Any(d => d.UsedPercentage >= 95);
                bool diskWarning  = !diskCritical && diskList.Any(d => d.UsedPercentage >= 80);

                if (diskCritical)
                {
                    var affected = string.Join(", ",
                        diskList.Where(d => d.UsedPercentage >= 95)
                                .Select(d => $"{d.Drive} {d.UsedPercentage}%"));
                    warnings.Add(new AnalysisWarning("HIGH", $"Disk critical: {affected}"));
                    score -= 20;
                }
                else if (diskWarning)
                {
                    var affected = string.Join(", ",
                        diskList.Where(d => d.UsedPercentage >= 80)
                                .Select(d => $"{d.Drive} {d.UsedPercentage}%"));
                    warnings.Add(new AnalysisWarning("MEDIUM", $"Disk usage warning: {affected}"));
                    score -= 10;
                }
            }

            // ── RAM analysis — FIXED: use OS-level free memory, not process-sum ─
            // The original code summed WorkingSet of all processes which is wrong:
            //   • shared pages counted multiple times
            //   • system/kernel processes missed when running without admin
            //   • ignores OS file-cache and driver allocations
            // FreeMemoryGB comes from Win32_OperatingSystem.FreePhysicalMemory (accurate).
            if (machine != null && machine.TotalMemoryGB > 0 && machine.FreeMemoryGB >= 0)
            {
                double memUsedPct = machine.MemoryUsedPercentage;
                string memDetail  = $"{machine.UsedMemoryGB:F1} GB / {machine.TotalMemoryGB:F1} GB";

                if (memUsedPct >= 90)
                {
                    warnings.Add(new AnalysisWarning("HIGH",
                        $"RAM usage critical: {memUsedPct:F1}% ({memDetail})"));
                    score -= 20;
                }
                else if (memUsedPct >= 70)
                {
                    warnings.Add(new AnalysisWarning("MEDIUM",
                        $"RAM usage elevated: {memUsedPct:F1}% ({memDetail})"));
                    score -= 10;
                }
            }
            else if (machine != null && machine.TotalMemoryGB > 0 && machine.FreeMemoryGB == 0)
            {
                // FreeMemoryGB absent (old collector) → fall back to process-sum with caveat
                var procList = (processes ?? Enumerable.Empty<ProcessInfo>()).ToList();
                double totalProcMb   = procList.Sum(p => p.MemoryMb);
                double totalMemMb    = machine.TotalMemoryGB * 1024.0;
                double memUsedPct    = totalMemMb > 0 ? (totalProcMb / totalMemMb) * 100.0 : 0;

                if (memUsedPct >= 90)
                {
                    warnings.Add(new AnalysisWarning("HIGH",
                        $"RAM usage high (estimate): {memUsedPct:F1}% — re-run collector for accurate data"));
                    score -= 20;
                }
                else if (memUsedPct >= 70)
                {
                    warnings.Add(new AnalysisWarning("MEDIUM",
                        $"RAM usage warning (estimate): {memUsedPct:F1}%"));
                    score -= 10;
                }
            }

            // ── Startup program count ───────────────────────────────────────────
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

            // ── Excessive Chrome processes ──────────────────────────────────────
            var procList2 = (processes ?? Enumerable.Empty<ProcessInfo>()).ToList();
            int chromeCount = procList2.Count(p =>
                string.Equals(p.Name, "chrome", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, "chrome.exe", StringComparison.OrdinalIgnoreCase));
            if (chromeCount > 20)
                warnings.Add(new AnalysisWarning("MEDIUM", $"Many Chrome processes detected: {chromeCount}"));

            // ── Long uptime ────────────────────────────────────────────────────
            if (machine?.BootTime != null)
            {
                if (TryParseNetDate(machine.BootTime, out var boot) ||
                    DateTime.TryParse(machine.BootTime, out boot))
                {
                    var uptime = DateTime.UtcNow - boot.ToUniversalTime();
                    if (uptime.TotalDays > 14)
                    {
                        warnings.Add(new AnalysisWarning("LOW",
                            $"Long uptime: {uptime.TotalDays:F1} days"));
                        score -= 5;
                    }
                }
            }

            // ── Developer environment findings ─────────────────────────────────
            if (devEnv != null)
                AnalyzeDevEnvironment(devEnv, devFindings, warnings, ref score);

            if (score < 0) score = 0;

            return new AnalysisResult
            {
                HealthScore     = score,
                Warnings        = warnings,
                Recommendations = BuildRecommendations(warnings, devFindings),
                DevFindings     = devFindings
            };
        }

        // ───────────────────────────────────────────────────────────────────────
        private static void AnalyzeDevEnvironment(
            DevEnvironmentInfo    devEnv,
            List<string>          devFindings,
            List<AnalysisWarning> warnings,
            ref int               score)
        {
            // WSL2 disk bloat
            if (devEnv.WslDetected && devEnv.WslDisks?.Count > 0)
            {
                double totalWsl = devEnv.WslDisks.Sum(w => w.SizeGB);
                devFindings.Add($"WSL2 detected: {devEnv.WslDisks.Count} distribution(s) using {totalWsl:F1} GB " +
                                "in virtual disks. VHDs expand but never shrink automatically.");
                if (totalWsl > 50)
                {
                    warnings.Add(new AnalysisWarning("MEDIUM",
                        $"WSL2 virtual disk(s) consuming {totalWsl:F1} GB — consider running 'wsl --shutdown' then Optimize-VHD"));
                    score -= 5;
                }
            }

            // Developer caches
            if (devEnv.TotalDevCacheSizeGB > 0)
            {
                var top = devEnv.DevCaches?
                    .Where(c => c.SizeGB >= 0.1)
                    .OrderByDescending(c => c.SizeGB)
                    .Take(4)
                    .Select(c => $"{c.Name}: {c.SizeGB:F1} GB");
                devFindings.Add($"Developer caches: {devEnv.TotalDevCacheSizeGB:F1} GB total" +
                                (top != null ? $" ({string.Join(", ", top)})" : string.Empty));
                if (devEnv.TotalDevCacheSizeGB > 10)
                {
                    warnings.Add(new AnalysisWarning("LOW",
                        $"Developer package caches consuming {devEnv.TotalDevCacheSizeGB:F1} GB"));
                    score -= 3;
                }
            }

            // Zombie processes
            if (devEnv.ZombieProcesses?.Count > 0)
            {
                var zombieGroups = devEnv.ZombieProcesses
                    .GroupBy(z => z.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => $"{g.Key} ×{g.Count()}");
                double zombieMem = devEnv.ZombieProcesses.Sum(z => z.MemoryMb) / 1024.0;
                devFindings.Add($"Orphaned developer processes: {string.Join(", ", zombieGroups)} " +
                                $"({zombieMem:F1} GB RAM)");
                if (devEnv.ZombieProcesses.Count >= 3)
                {
                    warnings.Add(new AnalysisWarning("LOW",
                        $"{devEnv.ZombieProcesses.Count} orphaned dev processes detected ({zombieMem:F1} GB RAM)"));
                    score -= 3;
                }
            }

            // LongPaths
            if (!devEnv.LongPathsEnabled)
                devFindings.Add("LongPathsEnabled registry key is NOT set — deep file paths (>260 chars) may fail.");

            // Missing key tools
            var missingTools = devEnv.PathTools?
                .Where(t => !t.Found &&
                            new[] { "git", "dotnet", "node", "docker" }.Contains(t.ToolName, StringComparer.OrdinalIgnoreCase))
                .Select(t => t.ToolName)
                .ToList();
            if (missingTools?.Count > 0)
                devFindings.Add($"Key tools not found in PATH: {string.Join(", ", missingTools)}");

            // Docker detected summary
            if (devEnv.DockerDetected && devEnv.DockerContainers?.Count > 0)
                devFindings.Add($"Docker: {devEnv.DockerContainers.Count} running container(s), " +
                                $"{devEnv.DockerImages?.Count ?? 0} image(s) pulled.");

            // PATH length warning
            if (devEnv.PathLength > 2048)
            {
                devFindings.Add($"PATH environment variable is very long ({devEnv.PathLength} chars) — " +
                                "can cause shell startup delays and tool resolution failures.");
            }
        }

        // ───────────────────────────────────────────────────────────────────────
        private static List<string> BuildRecommendations(
            List<AnalysisWarning> warnings,
            List<string>          devFindings)
        {
            var recs = new List<string>();

            foreach (var w in warnings)
            {
                var msg = (w.Message ?? string.Empty).ToLowerInvariant();
                if (msg.Contains("disk"))
                    recs.Add("Free up disk space (run Disk Cleanup or Storage Sense)");
                else if (msg.Contains("ram"))
                    recs.Add("Close unused applications, or consider upgrading RAM");
                else if (msg.Contains("startup"))
                    recs.Add("Disable unnecessary startup programs via Task Manager > Startup Apps");
                else if (msg.Contains("chrome"))
                    recs.Add("Close unnecessary Chrome tabs or background extensions");
                else if (msg.Contains("uptime") || msg.Contains("long uptime"))
                    recs.Add("Reboot to clear long-running state and apply pending updates");
                else if (msg.Contains("wsl2") || msg.Contains("virtual disk"))
                    recs.Add("Shrink WSL2 virtual disk: run 'wsl --shutdown' then Optimize-VHD in PowerShell");
                else if (msg.Contains("cache"))
                    recs.Add("Clear unused developer caches (npm, NuGet, Gradle) from the Dev Environment tab");
                else if (msg.Contains("orphaned"))
                    recs.Add("Kill orphaned developer processes from the Processes tab");
            }

            return recs.Distinct().ToList();
        }

        // ───────────────────────────────────────────────────────────────────────
        private static bool TryParseNetDate(string s, out DateTime dt)
        {
            dt = DateTime.MinValue;
            if (string.IsNullOrEmpty(s)) return false;
            var start = s.IndexOf('(');
            var end   = s.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var num = s.Substring(start + 1, end - start - 1);
                if (long.TryParse(num, out var ms))
                {
                    try { dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime; return true; }
                    catch { return false; }
                }
            }
            return false;
        }
    }
}
