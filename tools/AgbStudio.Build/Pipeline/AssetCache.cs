using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgbStudio.Build.Pipeline;

/// <summary>Hash cache (blueprint 11): an asset is converted again only when its input bytes, its options or
/// the converter version changed. The manifest lives in build/cache/manifest.json.</summary>
public sealed class AssetCache
{
    readonly string _path;
    readonly Dictionary<string, string> _entries;
    readonly HashSet<string> _seen = new();

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public AssetCache(string buildDir)
    {
        _path = Path.Combine(buildDir, "cache", "manifest.json");
        _entries = File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new()
            : new();
    }

    public static string Hash(params object[] parts)
    {
        using var sha = SHA256.Create();
        foreach (var p in parts)
        {
            var bytes = p switch
            {
                byte[] b => b,
                string s => Encoding.UTF8.GetBytes(s),
                _ => Encoding.UTF8.GetBytes(p.ToString() ?? ""),
            };
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            sha.TransformBlock([0], 0, 1, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!)[..32];
    }

    public static string HashFiles(IEnumerable<string> files, params object[] extra)
    {
        var parts = new List<object>();
        foreach (var f in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            parts.Add(f.Replace('\\', '/'));
            parts.Add(File.Exists(f) ? File.ReadAllBytes(f) : Array.Empty<byte>());
        }
        parts.AddRange(extra);
        return Hash(parts.ToArray());
    }

    /// <summary>True when the output for <paramref name="key"/> is current; otherwise the caller converts and calls Store.</summary>
    public bool IsCurrent(string key, string hash, IEnumerable<string> outputs)
    {
        _seen.Add(key);
        if (_entries.TryGetValue(key, out var h) && h == hash && outputs.All(File.Exists))
        {
            Hits++;
            return true;
        }
        Misses++;
        return false;
    }

    public void Store(string key, string hash) => _entries[key] = hash;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        foreach (var stale in _entries.Keys.Where(k => !_seen.Contains(k)).ToList()) _entries.Remove(stale);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Clear(string buildDir)
    {
        var dir = Path.Combine(buildDir, "cache");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    public static long Size(string buildDir) =>
        Directory.Exists(buildDir) ? new DirectoryInfo(buildDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
}
