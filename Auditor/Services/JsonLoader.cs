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
        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public JsonLoader(string dataDir)
        {
            _dataDir = Path.GetFullPath(dataDir);
        }

        private string? FindFile(string baseName)
        {
            if (!Directory.Exists(_dataDir)) return null;
            var files = Directory.GetFiles(_dataDir, "*.json", SearchOption.TopDirectoryOnly);
            return files.FirstOrDefault(f => Path.GetFileName(f).Equals(baseName + ".json", StringComparison.OrdinalIgnoreCase) || Path.GetFileNameWithoutExtension(f).Equals(baseName, StringComparison.OrdinalIgnoreCase));
        }

        public T? LoadSingle<T>(string baseName)
        {
            var file = FindFile(baseName);
            if (file == null) return default;
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
            catch
            {
                return default;
            }
        }

        public IEnumerable<T> LoadMany<T>(string baseName)
        {
            var file = FindFile(baseName);
            if (file == null) return Enumerable.Empty<T>();
            var txt = File.ReadAllText(file);
            try
            {
                if (txt.TrimStart().StartsWith("["))
                {
                    return JsonSerializer.Deserialize<List<T>>(txt, _opts) ?? Enumerable.Empty<T>();
                }
                var single = JsonSerializer.Deserialize<T>(txt, _opts);
                return single != null ? new[] { single } : Enumerable.Empty<T>();
            }
            catch
            {
                return Enumerable.Empty<T>();
            }
        }
    }
}
