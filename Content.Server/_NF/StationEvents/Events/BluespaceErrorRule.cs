using System.Numerics;
using Content.Server.Cargo.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Content.Server._NF.Salvage;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Server.GameTicking;
using Content.Server.Procedural;
using Robust.Shared.Prototypes;
using Content.Shared.Salvage;
using Content.Server.Maps.NameGenerators;
using Content.Server.StationEvents.Events;
using Content.Server._NF.Station.Systems;
using Content.Server._NF.StationEvents.Components;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server._Mono.StationEvents;
using Content.Server._Mono.GridClaimer;
using Content.Server._Mono.Store.Components;
using Content.Server._Mono.Store;
using Newtonsoft.Json;
using Robust.Server.Physics; // LuaM
// LuaM-start
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared.GameTicking;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;
using Robust.Shared.Profiling;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;
// LuaM-end

namespace Content.Server._NF.StationEvents.Events;

public sealed partial class BluespaceErrorRule : StationEventSystem<BluespaceErrorRuleComponent>
{
    NanotrasenNameGenerator _nameGenerator = new();
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DungeonSystem _dungeon = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private SharedSalvageSystem _salvage = default!;
    [Dependency] private AutoExtendRuleSystem _autoExtend = default!;
    [Dependency] private CurrencyInjectionSystem _currencyInjection = default!;
    // LuaM-start
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private IResourceManager _resources = default!;

    // -- Конец события растянут по тикам: оценка грида, его удаление, затем удаление карт
    private sealed class EndWork(EntityUid rule, BluespaceErrorRuleComponent component)
    {
        public readonly EntityUid Rule = rule;
        public readonly BluespaceErrorRuleComponent Component = component;
        public readonly Queue<EntityUid> Grids = new();
        public readonly HashSet<MapId> ClaimedMaps = new();
        public double? GridValue;
    }

    private readonly Queue<EndWork> _endWork = new();
    // LuaM-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit); // LuaM
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _endWork.Clear()); // LuaM
    }

    // LuaM-start: pieces broken off an event grid belong to the event, so they're deleted with it
    private void OnGridSplit(ref GridSplitEvent args)
    {
        var query = EntityQueryEnumerator<BluespaceErrorRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule) || !comp.GridsUid.Contains(args.Grid))
                continue;

            comp.GridsUid.AddRange(args.NewGrids);

            // the pieces of a claimable grid can be claimed too, or they'd be deleted with the event
            if (HasComp<ClaimableGridComponent>(args.Grid))
            {
                foreach (var grid in args.NewGrids)
                    EnsureComp<ClaimableGridComponent>(grid);
            }

            break; // a grid belongs to one event only
        }
    }
    // LuaM-end

    // LuaM: the grid value is also needed by the currency injection, not only by the reward accounts
    private bool NeedsAppraisal(EntityUid uid, BluespaceErrorRuleComponent component)
    {
        return component.RewardAccounts.Count > 0 || HasComp<CurrencyInjectionOnBluespaceErrorComponent>(uid);
    }

    protected override void Started(EntityUid uid, BluespaceErrorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!_map.TryGetMap(GameTicker.DefaultMap, out var mapUid))
            return;

        // LuaM-start: one grid per tick in ActiveTick, the map file is parsed in the background
        component.SpawnCoords = new EntityCoordinates(mapUid.Value, Vector2.Zero);

        // Spawn on a dummy map and try to FTL if possible, otherwise dump it.
        _map.CreateMap(out var mapId);
        component.SpawnMap = mapId;
        component.PendingSpawns.Clear();

        foreach (var group in component.Groups.Values)
        {
            var count = _random.Next(group.MinCount, group.MaxCount + 1);

            for (var i = 0; i < count; i++)
            {
                var pending = new PendingBluespaceSpawn(group, i);
                if (group is BluespaceGridSpawnGroup gridGroup)
                {
                    if (PickGridPath(gridGroup, i) is not { } path)
                        continue;

                    pending.Path = path;
                    StartParsing(pending, path);
                }

                component.PendingSpawns.Enqueue(pending);
            }
        }

        if (component.PendingSpawns.Count == 0)
            FinishSpawning(component);
        // LuaM-end
    }

    // LuaM-start
    protected override void ActiveTick(EntityUid uid, BluespaceErrorRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (!component.PendingSpawns.TryPeek(out var pending))
            return;

        if (pending.Spawned is { } loaded)
        {
            component.PendingSpawns.Dequeue();
            if (!TerminatingOrDeleted(loaded))
            {
                AppraiseStart(uid, component, loaded);
                if (component.ExtendIfPopulated)
                    _autoExtend.AutoExtend(uid, loaded);
            }
        }
        else
        {
            if (pending.Parsing)
                return;

            if (component.SpawnMap is not { } mapId || !_mapSystem.MapExists(mapId))
            {
                FinishSpawning(component);
                return;
            }

            if (SpawnFromGroup(uid, component, pending, mapId, out var spawned)
                && (NeedsAppraisal(uid, component) || component.ExtendIfPopulated))
                pending.Spawned = spawned;
            else
                component.PendingSpawns.Dequeue();
        }

        if (component.PendingSpawns.Count == 0)
            FinishSpawning(component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_endWork.TryPeek(out var work))
            return;

        if (work.Grids.TryPeek(out var grid))
        {
            if (work.GridValue == null && NeedsAppraisal(work.Rule, work.Component))
            {
                work.GridValue = AppraiseForEnd(grid);
                return;
            }

            work.Grids.Dequeue();
            EndGrid(work.Rule, work.Component, grid, work.GridValue ?? 0);
            work.GridValue = null;
            return;
        }

        _endWork.Dequeue();
        DeleteMaps(work);
    }

    private void FinishSpawning(BluespaceErrorRuleComponent component)
    {
        component.PendingSpawns.Clear();
        if (component.SpawnMap is { } mapId && _mapSystem.MapExists(mapId))
            _map.DeleteMap(mapId);

        component.SpawnMap = null;
    }

    // -- Файл читается в основном потоке: менеджер ресурсов не потокобезопасен, в фоне только разбор YAML
    private void StartParsing(PendingBluespaceSpawn pending, ResPath path)
    {
        if (!_resources.TryContentFileRead(path.ToRootedPath(), out var stream))
            return;

        string text;
        using (var reader = new StreamReader(stream))
            text = reader.ReadToEnd();

        pending.Parsing = true;
        Task.Run(() =>
        {
            try
            {
                var documents = DataNodeParser.ParseYamlStream(new StringReader(text)).ToArray();
                pending.Parsed = documents.Length == 1 ? documents[0].Root as MappingDataNode : null;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to parse bluespace grid {path}: {e}");
            }
            finally
            {
                pending.Parsing = false;
            }
        });
    }

    private void AppraiseStart(EntityUid uid, BluespaceErrorRuleComponent component, EntityUid spawned)
    {
        if (!NeedsAppraisal(uid, component))
            return;

        using (_prof.Group("Bluespace appraise"))
            component.StartingValue += _pricing.AppraiseGrid(spawned);
    }

    private bool SpawnFromGroup(EntityUid uid, BluespaceErrorRuleComponent component, PendingBluespaceSpawn pending, MapId mapId, out EntityUid spawned)
    {
        var group = pending.Group;
        var i = pending.Index;
        var spawnCoords = component.SpawnCoords;

        if (group.MinimumDistance > 0f)
        {
            spawnCoords = spawnCoords.WithPosition(_random.NextVector2(group.MinimumDistance, group.MaximumDistance));
            component.SpawnCoords = spawnCoords;
        }

        switch (group)
        {
            case BluespaceDungeonSpawnGroup dungeon:
                if (!TryDungeonSpawn(spawnCoords, component, ref dungeon, i, out spawned))
                    return false;

                break;
            case BluespaceGridSpawnGroup grid:
                var parsed = pending.Parsed;
                pending.Parsed = null;
                if (pending.Path is not { } path || !TryGridSpawn(spawnCoords, uid, mapId, path, parsed, grid, out spawned))
                {
                    spawned = EntityUid.Invalid;
                    return false;
                }

                break;
            default:
                throw new NotImplementedException();
        }

        if (group.NameLoc != null && group.NameLoc.Count > 0)
        {
            _metadata.SetEntityName(spawned, Loc.GetString(_random.Pick(group.NameLoc)));
        }
        else if (_protoManager.TryIndex(group.NameDataset, out var dataset))
        {
            string gridName;
            switch (group.NameDatasetType)
            {
                case BluespaceDatasetNameType.FTL:
                    gridName = _salvage.GetFTLName(dataset, _random.Next());
                    break;
                case BluespaceDatasetNameType.Nanotrasen:
                    gridName = _nameGenerator.FormatName(Loc.GetString(_random.Pick(dataset.Values)) + " {1}"); // We need the prefix.
                    break;
                case BluespaceDatasetNameType.Verbatim:
                default:
                    gridName = Loc.GetString(_random.Pick(dataset.Values));
                    break;
            }

            _metadata.SetEntityName(spawned, gridName);
        }

        if (group.NameWarp)
        {
            bool? adminOnly = group.HideWarp ? true : null;
            _renameWarps.SyncWarpPointsToGrid(spawned, forceAdminOnly: adminOnly);
        }

        EntityManager.AddComponents(spawned, group.AddComponents);

        component.GridsUid.Add(spawned);
        return true;
    }

    private ResPath? PickGridPath(BluespaceGridSpawnGroup group, int i)
    {
        if (group.Paths.Count == 0)
        {
            Log.Error($"Found no paths for GridSpawn");
            return null;
        }

        // Enforce randomness with some round-robin-ish behaviour
        int maxIndex = group.Paths.Count - (i % group.Paths.Count);
        int index = _random.Next(maxIndex);
        var path = group.Paths[index];
        // Move selected item to the end of the list
        group.Paths.RemoveAt(index);
        group.Paths.Add(path);
        return path;
    }
    // LuaM-end

    private bool TryDungeonSpawn(EntityCoordinates spawnCoords, BluespaceErrorRuleComponent component, ref BluespaceDungeonSpawnGroup group, int i, out EntityUid spawned)
    {
        spawned = EntityUid.Invalid;

        // handle empty prototype list, _random.Pick throws
        if (group.Protos.Count <= 0)
            return false;

        // Enforce randomness with some round-robin-ish behaviour
        int maxIndex = group.Protos.Count - (i % group.Protos.Count);
        int index = _random.Next(maxIndex);
        var dungeonProtoId = group.Protos[index];
        // Move selected item to the end of the list
        group.Protos.RemoveAt(index);
        group.Protos.Add(dungeonProtoId);

        if (!_protoManager.TryIndex(dungeonProtoId, out var dungeonProto))
        {
            return false;
        }

        _mapSystem.CreateMap(out var mapId);

        var spawnedGrid = _mapSystem.CreateGridEntity(mapId);

        _transform.SetMapCoordinates(spawnedGrid, new MapCoordinates(Vector2.Zero, mapId));
        _dungeon.GenerateDungeon(dungeonProto, dungeonProto.ID, spawnedGrid.Owner, spawnedGrid.Comp, Vector2i.Zero, _random.Next(), spawnCoords); // Frontier: add dungeonProto.ID

        spawned = spawnedGrid.Owner;
        component.MapsUid.Add(mapId);
        return true;
    }

    // LuaM-start: the path is picked in Started, the file may already be parsed in the background
    private bool TryGridSpawn(EntityCoordinates spawnCoords, EntityUid stationUid, MapId mapId, ResPath path, MappingDataNode? parsed, BluespaceGridSpawnGroup group, out EntityUid spawned)
    {
        spawned = EntityUid.Invalid;

        Entity<MapGridComponent>? ent = null;
        using (_prof.Group("Bluespace load grid"))
        {
            if (parsed == null)
            {
                _loader.TryLoadGrid(mapId, path, out ent);
            }
            else
            {
                var options = new MapLoadOptions
                {
                    MergeMap = mapId,
                    DeserializationOptions = DeserializationOptions.Default,
                    ExpectedCategory = FileCategory.Grid,
                };

                if (_loader.TryLoadGeneric(parsed, path.ToString(), out var result, options))
                {
                    if (result.Grids.Count == 1)
                        ent = result.Grids.Single();
                    else
                        _loader.Delete(result);
                }
            }
        }

        if (ent == null)
        {
            Log.Error($"Error loading gridspawn for {ToPrettyString(stationUid)} / {path}");
            return false;
        }

        if (HasComp<ShuttleComponent>(ent.Value))
            _shuttle.TryFTLProximity(ent.Value.Owner, spawnCoords);

        if (group.NameGrid)
            _metadata.SetEntityName(ent.Value, path.FilenameWithoutExtension);

        spawned = ent.Value;
        return true;
    }
    // LuaM-end

    protected override void Ended(EntityUid uid, BluespaceErrorRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        // LuaM-start: grids still waiting for their starting appraisal are appraised now, otherwise the integrity check divides by an incomplete value
        foreach (var pending in component.PendingSpawns)
        {
            if (pending.Spawned is { } spawned && !TerminatingOrDeleted(spawned))
                AppraiseStart(uid, component, spawned);
        }

        FinishSpawning(component);
        // LuaM-end

        if (component.GridsUid == null)
            return;

        var work = new EndWork(uid, component); // LuaM

        foreach (var componentGridUid in component.GridsUid)
        {
            if (TerminatingOrDeleted(componentGridUid)) // LuaM: a piece of the event grid can be destroyed before the event ends
                continue;

            if (!TryComp(componentGridUid, out TransformComponent? gridTransform))
            {
                Log.Error("bluespace error objective was missing transform component");
                continue; // LuaM: return > continue
            }

            if (gridTransform.GridUid is not EntityUid gridUid)
            {
                Log.Error("bluespace error has no associated grid?");
                continue; // LuaM: return > continue
            }

            // don't delete it if claimed
            if (TryComp<ClaimableGridComponent>(componentGridUid, out var claimable) && claimable.Claimed)
            {
                work.ClaimedMaps.Add(gridTransform.MapID); // LuaM
                continue; // LuaM: return > continue
            }

            if (component.DeleteGridsOnEnd)
                work.Grids.Enqueue(gridUid); // LuaM: appraised and deleted one step per tick in Update
        }

        _endWork.Enqueue(work); // LuaM: the maps are deleted after the grids
    }

    // LuaM-start
    private bool IsClaimed(EntityUid gridUid)
    {
        return TryComp<ClaimableGridComponent>(gridUid, out var claimable) && claimable.Claimed;
    }

    private double AppraiseForEnd(EntityUid gridUid)
    {
        if (TerminatingOrDeleted(gridUid) || IsClaimed(gridUid))
            return 0;

        // -- Игроков на борту перед удалением снимают с грида, поэтому они и их вещи не входят в оценку
        var aboard = _linkedLifecycleGrid.GetEntitiesToReparent(gridUid).Select(mob => mob.Entity.Owner).ToHashSet();
        using var appraise = _prof.Group("Bluespace end appraise");
        return _pricing.AppraiseGrid(gridUid, child => !aboard.Contains(child));
    }

    private void EndGrid(EntityUid uid, BluespaceErrorRuleComponent component, EntityUid gridUid, double gridValue)
    {
        // -- Грид могли захватить, пока он ждал своей очереди на удаление
        if (TerminatingOrDeleted(gridUid) || IsClaimed(gridUid) || !TryComp(gridUid, out TransformComponent? gridTransform))
            return;

        // Handle mobrestrictions getting deleted
        var query = AllEntityQuery<NFSalvageMobRestrictionsComponent>();
        while (query.MoveNext(out var salvUid, out var salvMob))
        {
            if (!salvMob.DespawnIfOffLinkedGrid)
            {
                var transform = Transform(salvUid);
                if (transform.GridUid != salvMob.LinkedGridEntity)
                {
                    RemComp<NFSalvageMobRestrictionsComponent>(salvUid);
                    continue;
                }
            }

            if (gridTransform.GridUid == salvMob.LinkedGridEntity)
                QueueDel(salvUid);
        }

        var playerMobs = _linkedLifecycleGrid.GetEntitiesToReparent(gridUid);
        foreach (var mob in playerMobs)
        {
            _transform.DetachEntity(mob.Entity.Owner, mob.Entity.Comp);
        }

        // Mono: currency injections
        if (TryComp<CurrencyInjectionOnBluespaceErrorComponent>(uid, out var comp))
        {
            var childQuery = Transform(gridUid).ChildEnumerator;
            var requiredEntitiesFound = true;
            foreach (var entProtoId in comp.RequiredEntities)
            {
                requiredEntitiesFound = false;
                while (childQuery.MoveNext(out var entity))
                {
                    var metaData = MetaData(entity);
                    if (metaData.EntityPrototype != null && entProtoId.Equals(metaData.EntityPrototype.ID))
                    {
                        requiredEntitiesFound = true;
                        break;
                    }
                }

                if (!requiredEntitiesFound)
                    break;
            }

            if (component.StartingValue > 0 && gridValue / component.StartingValue > comp.IntegrityRequirement && requiredEntitiesFound) // good job!
                _currencyInjection.InjectCurrency(comp.Company, comp.Amount);
        }

        // Deletion has to happen before grid traversal re-parents players.
        using (_prof.Group("Bluespace end delete"))
            Del(gridUid);

        foreach (var mob in playerMobs)
        {
            _transform.SetCoordinates(mob.Entity.Owner, new EntityCoordinates(mob.MapUid, mob.MapPosition));
        }

        foreach (var (account, rewardCoeff) in component.RewardAccounts)
        {
            var reward = (int)(gridValue * rewardCoeff);
            _bank.TrySectorDeposit(account, reward, LedgerEntryType.BluespaceReward);
        }
    }

    private void DeleteMaps(EndWork work)
    {
        foreach (var gridUid in work.Component.GridsUid)
        {
            if (!TerminatingOrDeleted(gridUid) && IsClaimed(gridUid))
                work.ClaimedMaps.Add(Transform(gridUid).MapID);
        }

        foreach (var mapId in work.Component.MapsUid)
        {
            if (!work.ClaimedMaps.Contains(mapId) && _map.MapExists(mapId))
                _map.DeleteMap(mapId);
        }
    }
    // LuaM-end
}
