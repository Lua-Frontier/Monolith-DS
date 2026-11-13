/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Content.Server._LuaM.Guidebook;

/// <summary>Disk-only helpers shared with the isolated MapRenderer executable.</summary>
public static class GuidebookRenderFiles
{
    public const int FormatVersion = 1;
    public static readonly Regex GuideMapRegex = new("<GuideMap\\s+Map=\"([^\"]+)\"", RegexOptions.Compiled);
    public sealed class Index
    {
        public int Format { get; set; } = FormatVersion;
        public string Assets { get; set; } = "";
        public Dictionary<string, Entry> Maps { get; set; } = new();
    }
    public sealed class Entry
    {
        public string Version { get; set; } = "";
        public List<string> Parts { get; set; } = new();
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string SourceHash(string path) => Hash(Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n")));
    public static string MapVersion(string assets, string file) => Hash(Encoding.UTF8.GetBytes(assets + SourceHash(file)));
    public static string Cache(string root) => Path.Combine(root, "GuidebookRenders");
    public static string ImagePath(string cache, string version, int part) => Path.Combine(cache, "map-" + version + "-" + part + ".png");
    public static bool IsVersion(string version) => version.Length == 64 && version.All(Uri.IsHexDigit);
    public static Index? ReadIndex(string cache)
    {
        try { return JsonSerializer.Deserialize<Index>(File.ReadAllText(Path.Combine(cache, "index.json"))); }
        catch (Exception e) when (e is IOException or JsonException) { return null; }
    }
    public static void WriteIndex(string cache, Index index)
    {
        Directory.CreateDirectory(cache);
        var temporary = Path.Combine(cache, "index-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(index));
        File.Move(temporary, Path.Combine(cache, "index.json"), true);
    }
    public static string AssetsVersion(string resources, string fallback, string tools)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string label, byte[] data) { hash.AppendData(Encoding.UTF8.GetBytes(label + "\0")); hash.AppendData(SHA256.HashData(data)); }
        Add("format", BitConverter.GetBytes(FormatVersion));
        foreach (var folder in new[] { "Prototypes", "Textures", "Shaders" })
        {
            var directory = Directory.Exists(Path.Combine(resources, folder)) ? Path.Combine(resources, folder) : Path.Combine(fallback, folder);
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (Path.GetFileName(file).StartsWith('.')) continue;
                var relative = folder + "/" + Path.GetRelativePath(directory, file).Replace('\\', '/');
                var text = new[] { ".yml", ".yaml", ".json", ".swsl", ".txt", ".md" }.Contains(Path.GetExtension(file).ToLowerInvariant());
                var data = text ? Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n", "\n")) : File.ReadAllBytes(file);
                Add(relative, data);
            }
        }
        // Visual systems also live in fork modules and the loader/test harness.
        // Hash all shipped managed dependencies, not only the base content DLLs.
        foreach (var file in Directory.EnumerateFiles(tools, "*.dll").OrderBy(p => p, StringComparer.Ordinal))
            Add(Path.GetFileName(file), File.ReadAllBytes(file));
        var guides = Directory.Exists(Path.Combine(resources, "ServerInfo")) ? Path.Combine(resources, "ServerInfo") : Path.Combine(fallback, "ServerInfo");
        if (Directory.Exists(guides))
        {
            var paths = Directory.EnumerateFiles(guides, "*.xml", SearchOption.AllDirectories)
                .SelectMany(p => GuideMapRegex.Matches(File.ReadAllText(p)).Select(m => m.Groups[1].Value))
                .Distinct().OrderBy(p => p, StringComparer.Ordinal);
            Add("guide-map-list", Encoding.UTF8.GetBytes(string.Join('\n', paths)));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
