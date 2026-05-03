using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mdv.Services;

/// JSON-backed key/value store. Replaces Windows.Storage.ApplicationData.LocalSettings
/// which only works in packaged (MSIX) apps.
public static class Settings
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mdv", "settings.json");
    private static readonly object _lock = new();
    private static Dictionary<string, JsonElement>? _cache;

    private static Dictionary<string, JsonElement> Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                _cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
            }
            else _cache = new();
        }
        catch { _cache = new(); }
        return _cache;
    }

    public static T? Get<T>(string key, T? fallback = default)
    {
        lock (_lock)
        {
            var dict = Load();
            if (!dict.TryGetValue(key, out var el)) return fallback;
            try
            {
                if (typeof(T) == typeof(string)) return (T)(object)(el.GetString() ?? "");
                if (typeof(T) == typeof(bool)) return (T)(object)el.GetBoolean();
                if (typeof(T) == typeof(int)) return (T)(object)el.GetInt32();
                if (typeof(T) == typeof(double)) return (T)(object)el.GetDouble();
                return JsonSerializer.Deserialize<T>(el.GetRawText());
            }
            catch { return fallback; }
        }
    }

    public static void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            var dict = Load();
            var el = JsonSerializer.SerializeToElement(value);
            dict[key] = el;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
