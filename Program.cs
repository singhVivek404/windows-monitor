using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WindowsMonitor
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "diagnostics.ps1"),
                Path.Combine(AppContext.BaseDirectory, "diagnostics.ps1"),
                Path.Combine(AppContext.BaseDirectory, "..", "diagnostics.ps1"),
                "diagnostics.ps1"
            };

            string? scriptPath = null;
            foreach (var c in candidates)
            {
                try
                {
                    if (File.Exists(c))
                    {
                        scriptPath = Path.GetFullPath(c);
                        break;
                    }
                }
                catch { }
            }

            if (scriptPath == null)
            {
                Console.Error.WriteLine("diagnostics.ps1 not found in common locations.");
                return 1;
            }

            Console.WriteLine($"Running diagnostics script: {scriptPath}");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Console.Error.WriteLine("Failed to start PowerShell process.");
                return 2;
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.Error.WriteLine("PowerShell error output:");
                Console.Error.WriteLine(stderr);
            }

            try
            {
                using var doc = JsonDocument.Parse(stdout);
                var options = new JsonSerializerOptions { WriteIndented = true };
                Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, options));
                return 0;
            }
            catch (Exception)
            {
                Console.WriteLine("PowerShell output (non-JSON or parse failed):");
                Console.WriteLine(stdout);
                return 3;
            }
        }
    }
}
