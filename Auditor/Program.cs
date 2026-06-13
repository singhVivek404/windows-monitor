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
            return AuditorRunner.Run(dataDir, null, s => Console.WriteLine(s));
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
