using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Decals;
using Content.Server.Procedural;
using Content.Shared._LuaM.Procedural.GridAssembly;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Server.GameStates;
using Robust.Server.Physics;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Procedural.GridAssembly;

/// <summary>
/// Generates a procedural base grid, creates module grids, and copies their tiles into one final grid.
/// All layout and spawned content is configured by a <see cref="GridAssemblyPrototype"/>.
/// </summary>
public sealed partial class GridAssemblySystem : EntitySystem
{
    [Dependency] private DungeonSystem _dungeon = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private GridFixtureSystem _gridFixtures = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public async Task<GridAssemblyResult> GenerateAsync(
        ProtoId<GridAssemblyPrototype> prototypeId,
        MapCoordinates anchor,
        int seed)
    {
        var prototype = _prototypes.Index(prototypeId);
        var mainGrid = _mapManager.CreateGridEntity(anchor.MapId);
        _transform.SetMapCoordinates(mainGrid,
            new MapCoordinates(anchor.Position + prototype.WorldOffset, anchor.MapId));

        try
        {
            if (prototype.Dungeon is { } dungeonId)
            {
                var dungeon = _prototypes.Index(dungeonId);
                await _dungeon.GenerateDungeonAsync(
                    dungeon,
                    $"{prototype.ID}-{seed}",
                    mainGrid.Owner,
                    mainGrid.Comp,
                    Vector2i.Zero,
                    seed);
            }

            if (Deleted(mainGrid.Owner))
                throw new InvalidOperationException($"Grid assembly {prototype.ID} base grid was deleted during generation.");

            var moduleOffsets = PlaceModules(prototype, seed);
            ConnectModules(mainGrid, prototype, moduleOffsets);

            foreach (var module in prototype.Modules)
            {
                var floorTile = module.FloorTile ?? prototype.ModuleFloorTile;
                CreateAndMergeModule(mainGrid, module, moduleOffsets[module.Id], floorTile);
            }

            ClearModuleInteriors(mainGrid, prototype, moduleOffsets);
            var spawned = SpawnModuleEntities(mainGrid, prototype, moduleOffsets);

            Log.Info($"Generated grid assembly {prototype.ID} on grid {mainGrid.Owner} from {prototype.Modules.Count} module grids.");
            return new GridAssemblyResult(mainGrid, moduleOffsets, spawned);
        }
        catch
        {
            if (!Deleted(mainGrid.Owner))
                QueueDel(mainGrid.Owner);

            throw;
        }
    }

    private static Dictionary<string, Vector2> PlaceModules(GridAssemblyPrototype prototype, int seed)
    {
        var random = new Random(seed);
        var result = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in prototype.Modules)
        {
            if (string.IsNullOrWhiteSpace(module.Id))
                throw new InvalidOperationException($"Grid assembly {prototype.ID} has a module with an empty ID.");

            if (result.ContainsKey(module.Id))
                throw new InvalidOperationException($"Grid assembly {prototype.ID} has duplicate module ID {module.Id}.");

            if (module.GridPath == null && module.HalfSize == null)
            {
                throw new InvalidOperationException(
                    $"Grid assembly {prototype.ID} module {module.Id} must define either gridPath or halfSize.");
            }

            if (module.HalfSize is { } halfSize && (halfSize.X < 0 || halfSize.Y < 0))
                throw new InvalidOperationException($"Grid assembly {prototype.ID} module {module.Id} has a negative size.");

            result.Add(module.Id, module.RandomPlacement == null
                ? module.Offset
                : FindRandomOffset(prototype, module, module.RandomPlacement, result, random));
        }

        return result;
    }

    private static Vector2 FindRandomOffset(
        GridAssemblyPrototype prototype,
        GridAssemblyModule module,
        GridAssemblyRadialPlacement placement,
        Dictionary<string, Vector2> placed,
        Random random)
    {
        if (placement.MinimumRadius < 0f || placement.MaximumRadius < placement.MinimumRadius)
            throw new InvalidOperationException($"Grid assembly {prototype.ID} module {module.Id} has invalid radial limits.");

        var origin = Vector2.Zero;
        if (!string.IsNullOrWhiteSpace(placement.Origin) && !placed.TryGetValue(placement.Origin, out origin))
        {
            throw new InvalidOperationException(
                $"Grid assembly {prototype.ID} module {module.Id} refers to unplaced origin {placement.Origin}.");
        }

        var attempts = Math.Max(1, prototype.PlacementAttempts);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var angle = (float) (random.NextDouble() * Math.Tau);
            var radius = placement.MinimumRadius +
                         (float) random.NextDouble() * (placement.MaximumRadius - placement.MinimumRadius);
            var candidate = origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var valid = true;

            foreach (var (otherId, minimumDistance) in placement.MinimumDistances)
            {
                if (!placed.TryGetValue(otherId, out var otherOffset))
                {
                    throw new InvalidOperationException(
                        $"Grid assembly {prototype.ID} module {module.Id} refers to unplaced module {otherId}.");
                }

                if (Vector2.Distance(candidate, otherOffset) >= minimumDistance)
                    continue;

                valid = false;
                break;
            }

            if (valid)
                return candidate;
        }

        throw new InvalidOperationException(
            $"Grid assembly {prototype.ID} could not place module {module.Id} after {attempts} attempts.");
    }

    private void CreateAndMergeModule(
        Entity<MapGridComponent> mainGrid,
        GridAssemblyModule module,
        Vector2 offset,
        ProtoId<ContentTileDefinition> floorPrototype)
    {
        var mapId = Transform(mainGrid).MapID;
        Entity<MapGridComponent>? moduleGrid = null;
        try
        {
            var mainWorld = _transform.GetWorldPosition(Transform(mainGrid));
            if (module.GridPath is { } gridPath)
            {
                if (!_mapLoader.TryLoadGrid(mapId, gridPath, out moduleGrid, offset: mainWorld))
                {
                    throw new InvalidOperationException(
                        $"Grid assembly module {module.Id} could not load gridPath {gridPath}.");
                }
            }
            else
            {
                var halfSize = module.HalfSize!.Value;
                moduleGrid = _mapManager.CreateGridEntity(mapId);
                _transform.SetMapCoordinates(moduleGrid.Value, new MapCoordinates(mainWorld, mapId));

                var tileDefinition = (ContentTileDefinition) _tileDefinitions[floorPrototype];
                var tile = new Tile(tileDefinition.TileId);
                var sourceTiles = new List<(Vector2i GridIndices, Tile Tile)>();
                for (var x = -halfSize.X; x <= halfSize.X; x++)
                {
                    for (var y = -halfSize.Y; y <= halfSize.Y; y++)
                        sourceTiles.Add((new Vector2i(x, y), tile));
                }

                _mapSystem.SetTiles(moduleGrid.Value, sourceTiles);
            }

            var gridOffset = new Vector2i((int) MathF.Round(offset.X), (int) MathF.Round(offset.Y));
            var decals = GetModuleDecals(moduleGrid.Value);
            _gridFixtures.Merge(
                mainGrid.Owner,
                moduleGrid.Value.Owner,
                gridOffset,
                module.Rotation,
                mainGrid.Comp,
                moduleGrid.Value.Comp);
            moduleGrid = null; // GridFixtureSystem.Merge deletes the source grid after transferring its entities.
            TransferDecals(mainGrid, decals, gridOffset, module.Rotation);
        }
        finally
        {
            if (moduleGrid is { } remainingGrid && !Deleted(remainingGrid.Owner))
                QueueDel(remainingGrid.Owner);
        }
    }

    private List<Decal> GetModuleDecals(Entity<MapGridComponent> moduleGrid)
    {
        if (!TryComp<DecalGridComponent>(moduleGrid.Owner, out var decalGrid))
            return [];

        return _decals
            .GetDecalsIntersecting(moduleGrid.Owner, moduleGrid.Comp.LocalAABB.Enlarged(1f), decalGrid)
            .Select(entry => entry.Decal)
            .ToList();
    }

    private void TransferDecals(
        Entity<MapGridComponent> mainGrid,
        IReadOnlyList<Decal> decals,
        Vector2i offset,
        Angle rotation)
    {
        if (decals.Count == 0)
            return;

        EnsureComp<DecalGridComponent>(mainGrid.Owner);
        var matrix = Matrix3Helpers.CreateTransform(offset, rotation);
        foreach (var decal in decals)
        {
            // Decals use the bottom-left tile corner, so rotate around the tile centre like GridFixtureSystem.Merge.
            var position = Vector2.Transform(decal.Coordinates + mainGrid.Comp.TileSizeHalfVector, matrix) -
                           mainGrid.Comp.TileSizeHalfVector;
            var angle = (decal.Angle + rotation).Reduced();
            if (!_decals.TryAddDecal(
                    decal.Id,
                    new EntityCoordinates(mainGrid.Owner, position),
                    out _,
                    decal.Color,
                    angle,
                    decal.ZIndex,
                    decal.Cleanable))
            {
                Log.Warning($"Could not transfer decal {decal.Id} while merging grid assembly module.");
            }
        }
    }

    private void ConnectModules(
        Entity<MapGridComponent> mainGrid,
        GridAssemblyPrototype prototype,
        IReadOnlyDictionary<string, Vector2> moduleOffsets)
    {
        foreach (var connection in prototype.Connections)
        {
            if (!moduleOffsets.TryGetValue(connection.From, out var from))
                throw new InvalidOperationException($"Grid assembly {prototype.ID} connection refers to missing module {connection.From}.");

            if (!moduleOffsets.TryGetValue(connection.To, out var to))
                throw new InvalidOperationException($"Grid assembly {prototype.ID} connection refers to missing module {connection.To}.");

            var floorPrototype = connection.FloorTile ?? prototype.ConnectionFloorTile;
            var definition = (ContentTileDefinition) _tileDefinitions[floorPrototype];
            var tile = new Tile(definition.TileId);
            var tiles = new Dictionary<Vector2i, Tile>();
            var width = Math.Max(1, connection.Width);
            var lower = -(width - 1) / 2;
            var upper = width / 2;
            var start = new Vector2i((int) MathF.Round(from.X), (int) MathF.Round(from.Y));
            var end = new Vector2i((int) MathF.Round(to.X), (int) MathF.Round(to.Y));

            foreach (var point in GetLine(start, end))
            {
                for (var x = lower; x <= upper; x++)
                {
                    for (var y = lower; y <= upper; y++)
                        tiles[point + new Vector2i(x, y)] = tile;
                }
            }

            _mapSystem.SetTiles(mainGrid, tiles.Select(entry => (entry.Key, entry.Value)).ToList());
        }
    }

    private void ClearModuleInteriors(
        Entity<MapGridComponent> mainGrid,
        GridAssemblyPrototype prototype,
        IReadOnlyDictionary<string, Vector2> moduleOffsets)
    {
        if (prototype.ClearEntityPrototypes.Count == 0)
            return;

        var mainWorld = _transform.GetWorldPosition(Transform(mainGrid));
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var metadata, out var transform))
        {
            if (transform.GridUid != mainGrid.Owner ||
                metadata.EntityPrototype?.ID is not { } entityPrototype ||
                !prototype.ClearEntityPrototypes.Contains(entityPrototype))
                continue;

            var local = _transform.GetWorldPosition(transform) - mainWorld;
            foreach (var module in prototype.Modules)
            {
                if (module.HalfSize is not { } halfSize)
                    continue;

                var relative = local - moduleOffsets[module.Id];
                var unrotated = (-module.Rotation).RotateVec(relative);
                if (MathF.Abs(unrotated.X) > halfSize.X + prototype.ClearPadding ||
                    MathF.Abs(unrotated.Y) > halfSize.Y + prototype.ClearPadding)
                    continue;

                QueueDel(uid);
                break;
            }
        }
    }

    private List<EntityUid> SpawnModuleEntities(
        Entity<MapGridComponent> mainGrid,
        GridAssemblyPrototype prototype,
        IReadOnlyDictionary<string, Vector2> moduleOffsets)
    {
        var spawned = new List<EntityUid>();
        foreach (var module in prototype.Modules)
        {
            var moduleOffset = moduleOffsets[module.Id];
            foreach (var entitySpawn in module.Entities)
            {
                var uid = Spawn(entitySpawn.Prototype,
                    new EntityCoordinates(mainGrid.Owner,
                        moduleOffset + module.Rotation.RotateVec(entitySpawn.Offset)));
                spawned.Add(uid);

                if (entitySpawn.GlobalPvs)
                    _pvs.AddGlobalOverride(uid);
            }
        }

        return spawned;
    }

    private static IEnumerable<Vector2i> GetLine(Vector2i start, Vector2i end)
    {
        var x = start.X;
        var y = start.Y;
        var dx = Math.Abs(end.X - start.X);
        var sx = start.X < end.X ? 1 : -1;
        var dy = -Math.Abs(end.Y - start.Y);
        var sy = start.Y < end.Y ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            yield return new Vector2i(x, y);
            if (x == end.X && y == end.Y)
                yield break;

            var twiceError = 2 * error;
            if (twiceError >= dy)
            {
                error += dy;
                x += sx;
            }

            if (twiceError <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }
}

public sealed class GridAssemblyResult
{
    public Entity<MapGridComponent> Grid { get; }
    public IReadOnlyDictionary<string, Vector2> ModuleOffsets { get; }
    public IReadOnlyList<EntityUid> SpawnedEntities { get; }

    public GridAssemblyResult(
        Entity<MapGridComponent> grid,
        IReadOnlyDictionary<string, Vector2> moduleOffsets,
        IReadOnlyList<EntityUid> spawnedEntities)
    {
        Grid = grid;
        ModuleOffsets = moduleOffsets;
        SpawnedEntities = spawnedEntities;
    }
}
