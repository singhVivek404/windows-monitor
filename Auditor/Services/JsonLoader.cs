using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WorkstationAuditor.Services
{
    public class JsonLoader
    {
        private readonly string _dataDir;
        private readonly JsonSerializerOptions _opts =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly Action<string>? _log;

        public JsonLoader(string dataDir, Action<string>? log = null)
        {
            _dataDir = Path.GetFullPath(dataDir);
            _log     = log;
        }

        private string? FindFile(string baseName)
        {
            if (!Directory.Exists(_dataDir)) return null;
            var files = Directory.GetFiles(_dataDir, "*.json", SearchOption.TopDirectoryOnly);
            return files.FirstOrDefault(f =>
                Path.GetFileName(f).Equals(baseName + ".json", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileNameWithoutExtension(f).Equals(baseName, StringComparison.OrdinalIgnoreCase));
        }

        public T? LoadSingle<T>(string baseName)
        {
            var file = FindFile(baseName);
            if (file == null)
            {
                _log?.Invoke($"[JsonLoader] File not found: {baseName}.json in {_dataDir}");
                return default;
            }

            var txt = File.ReadAllText(file);
            try
            {
                if (txt.TrimStart().StartsWith("["))
                {
                    var list = JsonSerializer.Deserialize<List<T>>(txt, _opts);
                    return list?.Count > 0 ? list[0] : default;
                }
                return JsonSerializer.Deserialize<T>(txt, _opts);
            }
            catch (JsonException jex)
            {
                // Surface the error instead of silently returning default
                _log?.Invoke($"[JsonLoader] Failed to parse {Path.GetFileName(file)}: {jex.Message}");
                return default;
            }
        }

        public IEnumerable<T> LoadMany<T>(string baseName)
        {
            var file = FindFile(baseName);
            if (file == null)
            {
                _log?.Invoke($"[JsonLoader] File not found: {baseName}.json in {_dataDir}");
                return Enumerable.Empty<T>();
            }

            var txt = File.ReadAllText(file);
            try
            {
                if (txt.TrimStart().StartsWith("["))
                    return JsonSerializer.Deserialize<List<T>>(txt, _opts) ?? Enumerable.Empty<T>();

                var single = JsonSerializer.Deserialize<T>(txt, _opts);
                return single != null ? new[] { single } : Enumerable.Empty<T>();
            }
            catch (JsonException jex)
            {
                _log?.Invoke($"[JsonLoader] Failed to parse {Path.GetFileName(file)}: {jex.Message}");
                return Enumerable.Empty<T>();
            }
        }
    }
}
