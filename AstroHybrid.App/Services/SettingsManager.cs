using System;
using System.IO;
using System.Text.Json;
using AstroHybrid.App.Models;

namespace AstroHybrid.App.Services
{
    public class SettingsManager
    {
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;
        private AppSettings? _cachedSettings;

        public SettingsManager(string rootDirectory)
        {
            _settingsPath = Path.Combine(rootDirectory, "Build", "astrohybrid-settings.json");
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public AppSettings Load()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            var parsed = TryLoadFile(_settingsPath) ?? TryLoadFile(_settingsPath + ".bak");
            if (parsed != null)
            {
                parsed.NormalizeRumble();
                _cachedSettings = parsed;
                return _cachedSettings;
            }

            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }

        private AppSettings? TryLoadFile(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public void Save(AppSettings settings)
        {
            _cachedSettings = settings;
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            var tempPath = _settingsPath + ".tmp";
            var backupPath = _settingsPath + ".bak";
            File.WriteAllText(tempPath, json);
            if (File.Exists(_settingsPath))
            {
                File.Replace(tempPath, _settingsPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }

        public string GetSettingsPath() => _settingsPath;
    }
}