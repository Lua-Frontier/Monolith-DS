using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Map.Events;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Maps;

/// <summary>
/// Client copy of the server MapMigrationSystem: applies the prototype renames and removals of the migration files
/// when the client loads a map or grid on its own, e.g. the shipyard preview. Without it ships with renamed
/// prototypes fail to load in the preview while the server loads them fine.
/// </summary>
public sealed partial class ClientMapMigrationSystem : EntitySystem
{
    [Dependency] private IResourceManager _resMan = default!;

    // keep in sync with Content.Server.Maps.MapMigrationSystem
    private static readonly string[] MigrationFiles = { "/migration.yml", "/nf_migration.yml", "/mono_migration.yml" };

    private List<MappingDataNode>? _mappings;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeEntityReadEvent>(OnBeforeReadEvent);
    }

    private void OnBeforeReadEvent(BeforeEntityReadEvent ev)
    {
        _mappings ??= ReadFiles();

        foreach (var mapping in _mappings)
        {
            foreach (var (key, value) in mapping)
            {
                if (value is not ValueDataNode valueNode)
                    continue;

                if (string.IsNullOrWhiteSpace(valueNode.Value) || valueNode.Value == "null")
                    ev.DeletedPrototypes.Add(key);
                else
                    ev.RenamedPrototypes.Add(key, valueNode.Value);
            }
        }
    }

    private List<MappingDataNode> ReadFiles()
    {
        var mappings = new List<MappingDataNode>();
        foreach (var file in MigrationFiles)
        {
            if (TryReadFile(file, out var mapping))
                mappings.Add(mapping);
        }

        return mappings;
    }

    private bool TryReadFile(string file, [NotNullWhen(true)] out MappingDataNode? mapping)
    {
        mapping = null;
        if (!_resMan.TryContentFileRead(new ResPath(file), out var stream))
            return false;

        using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
        if (DataNodeParser.ParseYamlStream(reader).FirstOrDefault() is not { } document)
            return false;

        mapping = (MappingDataNode) document.Root;
        return true;
    }
}
