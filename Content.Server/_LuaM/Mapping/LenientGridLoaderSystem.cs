using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Holiday;
using Content.Server.Maps;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Mapping;

public sealed class LenientGridLoaderSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string Placeholder = "LuaMLenientLoadPlaceholder";

    private readonly HashSet<string> _pendingMissing = new();

    private readonly HashSet<string> _substituted = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeEntityReadEvent>(OnBeforeEntityRead,
            after: new[] { typeof(MapMigrationSystem), typeof(HolidaySystem) });
    }

    private void OnBeforeEntityRead(BeforeEntityReadEvent ev)
    {
        foreach (var id in _pendingMissing)
        {
            if (ev.DeletedPrototypes.Contains(id) || ev.RenamedPrototypes.ContainsKey(id))
                continue;

            if (ev.RenamedPrototypes.TryAdd(id, Placeholder))
                _substituted.Add(id);
        }
    }

    public bool TryLoadGrid(
        MapId map,
        ResPath path,
        DeserializationOptions options,
        Vector2 offset,
        Angle rotation,
        [NotNullWhen(true)] out Entity<MapGridComponent>? grid,
        out LenientLoadReport report,
        out string? error)
    {
        grid = null;
        report = new LenientLoadReport();
        error = null;

        if (!_mapLoader.TryReadFile(path, out var data))
        {
            error = "file";
            return false;
        }

        Dictionary<string, int> missing;
        try
        {
            missing = CollectMissingPrototypes(data);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read entities of {path}: {e}");
            error = "format";
            return false;
        }

        var opts = new MapLoadOptions
        {
            MergeMap = map,
            Offset = offset,
            Rotation = rotation,
            DeserializationOptions = options,
            ExpectedCategory = FileCategory.Grid,
        };

        LoadResult? result;
        _substituted.Clear();
        _pendingMissing.UnionWith(missing.Keys);
        try
        {
            if (!_mapLoader.TryLoadGeneric(data, path.ToString(), out result, opts))
            {
                error = "load";
                return false;
            }
        }
        finally
        {
            _pendingMissing.Clear();
        }

        foreach (var id in _substituted)
        {
            report.Missing[id] = missing[id];
        }
        _substituted.Clear();

        if (result.Grids.Count != 1)
        {
            _mapLoader.Delete(result);
            error = "grids";
            return false;
        }

        ReplacePlaceholders(result, report);
        grid = result.Grids.Single();
        return true;
    }
    private Dictionary<string, int> CollectMissingPrototypes(MappingDataNode data)
    {
        var missing = new Dictionary<string, int>();
        var version = data.Get<MappingDataNode>("meta").Get<ValueDataNode>("format").AsInt();
        var key = version >= 4 ? "proto" : "type";

        foreach (var node in data.Get<SequenceDataNode>("entities").Cast<MappingDataNode>())
        {
            if (!node.TryGet<ValueDataNode>(key, out var protoNode) || string.IsNullOrWhiteSpace(protoNode.Value))
                continue;

            if (_proto.HasIndex<EntityPrototype>(protoNode.Value))
                continue;

            var count = version >= 4 && node.TryGet<SequenceDataNode>("entities", out var group) ? group.Count : 1;
            missing[protoNode.Value] = missing.GetValueOrDefault(protoNode.Value) + count;
        }

        return missing;
    }

    private void ReplacePlaceholders(LoadResult result, LenientLoadReport report)
    {
        var placeholders = new List<EntityUid>();
        foreach (var uid in result.Entities)
        {
            if (!TerminatingOrDeleted(uid) && MetaData(uid).EntityPrototype?.ID == Placeholder)
                placeholders.Add(uid);
        }

        var children = new List<EntityUid>();
        foreach (var uid in placeholders)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (TryComp<ContainerManagerComponent>(uid, out var manager))
            {
                foreach (var container in manager.Containers.Values.ToArray())
                {
                    foreach (var contained in _container.EmptyContainer(container, force: true))
                    {
                        _transform.DropNextTo(contained, uid);
                        report.Rescued++;
                    }
                }
            }

            children.Clear();
            var enumerator = Transform(uid).ChildEnumerator;
            while (enumerator.MoveNext(out var child))
            {
                children.Add(child);
            }

            foreach (var child in children)
            {
                _transform.DropNextTo(child, uid);
                report.Rescued++;
            }
            Del(uid);
            report.Removed++;
        }
    }
}

public sealed class LenientLoadReport
{
    public readonly Dictionary<string, int> Missing = new();

    public int Removed;
    public int Rescued;
}
