/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Content.Packaging._LuaM;

/// <summary>Only map sources needed by guidebook plans; ordinary server maps stay excluded.</summary>
public static class GuidebookMapResources
{
    private static readonly Regex GridPath = new(@"^\s*gridPath:\s*[""']?(/Maps/[^#""'\r\n]+\.yml)", RegexOptions.Multiline);

    public static IEnumerable<string> Find(string contentDir)
    {
        var resources = Path.GetFullPath(Path.Combine(contentDir, "Resources"));
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(resources, "ServerInfo", "_LuaM", "Guidebook"), "*.xml", SearchOption.AllDirectories))
        {
            foreach (var tag in XDocument.Load(file).Descendants("GuideMap"))
                Add(tag.Attribute("Map")?.Value);
        }
        foreach (var file in Directory.EnumerateFiles(Path.Combine(resources, "Prototypes"), "*.yml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("type: pointOfInterest", StringComparison.Ordinal)) continue;
            // POI paths are single-line YAML scalars, as in their prototypes.
            foreach (Match match in GridPath.Matches(text))
                Add(match.Groups[1].Value);
        }
        return paths.Order(StringComparer.Ordinal);

        void Add(string? path)
        {
            if (path == null || !path.StartsWith("/Maps/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal)) return;
            var relative = path.TrimStart('/');
            if (!File.Exists(Path.Combine(resources, relative)))
                throw new FileNotFoundException("Missing guidebook map: " + path);
            paths.Add(relative);
        }
    }
}
