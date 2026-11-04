/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Linq;
using System.Diagnostics;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.GameTicking;
using Content.Server._NF.GameRule;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._LuaM.Guidebook;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Access;
using Content.Shared.Research.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Guidebook;

internal sealed record GuidebookMappedEntity(
    string PrototypeId,
    string MapId,
    string MapName,
    SequenceDataNode? Components);

/// <summary>
/// Builds the guide from resolved prototypes and serialized map instances, without spawning entities.
/// </summary>
public sealed partial class GuidebookCatalogSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameMapManager _maps = default!;

    [Dependency] private IGameTiming _timing = default!;
    private readonly HashSet<ICommonSession> _waiting = new();
    private readonly Dictionary<NetUserId, TimeSpan> _lastRequest = new();
    private IEnumerator<bool>? _builder;
    private GuidebookCatalog? _building;
    private GuidebookCatalog? _cached;
    private string? _cachedPreset;
    private string? _cachedMainMap;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestGuidebookCatalogEvent>(OnRequest);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => InvalidateResources());
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetCache());
        Subs.CVar(_configuration, Content.Shared.CCVar.CCVars.WorldgenConfig, _ =>
        {
            InvalidateResources();
        }, false);
        // Selected map and preset are final here. Reuse the same bounded builder
        // as requests; no entities are spawned and clients receive nothing unsolicited.
        if (Environment.GetEnvironmentVariable("COLOSS_GUIDEBOOK_WORKER") != "1")
            SubscribeLocalEvent<RoundStartedEvent>(_ => EnsureSnapshot());
    }

    private void ResetCache(bool dropWaiters = false)
    {
        _builder?.Dispose(); _builder = null; _building = null; _cached = null;
        if (dropWaiters) _waiting.Clear();
        else _waiting.RemoveWhere(s => !s.Channel.IsConnected);
        _lastRequest.Clear();
    }

    public void InvalidateResources()
    {
        ResetCache();
        EnsureSnapshot();
        RaiseNetworkEvent(new GuidebookCatalogInvalidatedEvent());
    }

    public override void Shutdown() { ResetCache(dropWaiters: true); base.Shutdown(); }

    private void OnRequest(RequestGuidebookCatalogEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (_lastRequest.TryGetValue(session.UserId, out var previous) && _timing.CurTime - previous < TimeSpan.FromSeconds(1)) return;
        EnsureSnapshot();
        _lastRequest[session.UserId] = _timing.CurTime;
        if (_cached != null) { RaiseNetworkEvent(new UpdateGuidebookCatalogEvent(_cached), session); return; }
        _waiting.Add(session);
    }

    private void EnsureSnapshot()
    {
        var preset = (_gameTicker.CurrentPreset ?? _gameTicker.Preset)?.ID ?? "MonoStandard";
        var mainMap = _maps.GetSelectedMap()?.ID;
        if (_cachedPreset != preset || _cachedMainMap != mainMap)
        {
            var hadSnapshot = _cached != null;
            ResetCache(); _cachedPreset = preset; _cachedMainMap = mainMap;
            if (hadSnapshot) RaiseNetworkEvent(new GuidebookCatalogInvalidatedEvent());
        }
        if (_cached != null || _builder != null) return;
        _building = new GuidebookCatalog();
        _builder = BuildSnapshotSteps(_building).GetEnumerator();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_builder == null) return;
        try
        {
            var budget = System.Diagnostics.Stopwatch.StartNew();
            while (budget.Elapsed.TotalMilliseconds < 3)
            {
                if (_builder.MoveNext()) continue;
                _builder.Dispose(); _builder = null;
                _cached = _building; _building = null;
                foreach (var session in _waiting.Where(s => s.Channel.IsConnected))
                    RaiseNetworkEvent(new UpdateGuidebookCatalogEvent(_cached!), session);
                _waiting.Clear();
                break;
            }
        }
        catch (Exception exception)
        { Log.Error($"Guidebook catalogue failed: {exception}"); ResetCache(); }
    }

    // Synchronous entry point is only for tooling/tests. Player requests use the staged path.
    public GuidebookCatalog BuildSnapshot()
    {
        var result = new GuidebookCatalog();
        foreach (var _ in BuildSnapshotSteps(result)) { }
        return result;
    }

    private IEnumerable<bool> BuildSnapshotSteps(GuidebookCatalog result)
    {
        var mapPaths = new Dictionary<string, string>();
        var sitePaths = new Dictionary<string, bool>();
        var vouchers = _prototype.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.Components.ContainsKey("ShipyardVoucher"))
            .Select(p => (p.ID, Voucher: (ShipyardVoucherComponent) p.Components["ShipyardVoucher"].Component))
            .ToList();

        result.Vouchers = vouchers.Select(v => new GuidebookVoucherEntry
        {
            PrototypeId = v.ID, ConsoleType = v.Voucher.ConsoleType.ToString(), Uses = v.Voucher.RedemptionsLeft,
            CooldownSeconds = v.Voucher.Cooldown.TotalSeconds, DestroyOnEmpty = v.Voucher.DestroyOnEmpty,
        }).ToList();

        foreach (var vessel in _prototype.EnumeratePrototypes<VesselPrototype>().OrderBy(v => v.ID))
        {
            var path = vessel.ShuttlePath.ToRootedPath().ToString();
            var name = LocalizedText(vessel.LocName, vessel.Name);
            var entry = new GuidebookVesselEntry
            {
                Id = vessel.ID,
                Name = name,
                Description = LocalizedText(vessel.LocDescription, vessel.Description),
                Group = vessel.Group.ToString(),
                MapPath = path,
                Size = vessel.Category.ToString(),
                Classes = vessel.Classes.Select(c => c.ToString()).ToList(),
                Engines = vessel.Engines.Select(e => e.ToString()).ToList(),
                RequireCrew = vessel.RequireCrew || vessel.Classes.Contains(VesselClass.Capital),
                ActiveLimit = vessel.LimitActive,
                Price = vessel.Price,
                Purchasable = vessel.Purchasable,
                Vouchers = vouchers.Where(v => VoucherAllows(v.Voucher, vessel)).Select(v => v.ID).Order().ToList(),
            };
            result.Vessels.Add(entry);
            mapPaths.TryAdd(path, name);
        }

        var preset = (_gameTicker.CurrentPreset ?? _gameTicker.Preset)?.ID ?? "MonoStandard";
        var pois = _prototype.EnumeratePrototypes<PointOfInterestPrototype>().Where(p => !p.Abstract).ToList();
        // Local Sandbox has no sector preset: use the standard sector's reference catalogue.
        if (!pois.Any(p => p.SpawnGamePreset.Any(g => g.Id == preset))) preset = "MonoStandard";
        foreach (var poi in pois)
        {
            if (poi.SpawnGamePreset.Length > 0 && !poi.SpawnGamePreset.Any(p => p.Id == preset))
                continue;
            // Zero-chance custom groups cannot spawn; Required and Optional have separate rules.
            if (poi.SpawnGroup is not ("Required" or "Optional" or "CargoDepot" or "MarketStation") && poi.SpawnChance <= 0)
                continue;
            mapPaths.TryAdd(poi.GridPath.ToRootedPath().ToString(), poi.Name);
            sitePaths.TryAdd(poi.GridPath.ToRootedPath().ToString(), poi.HideWarp);
        }

        if (_maps.GetSelectedMap() is { } selected)
            mapPaths.TryAdd(selected.MapPath.ToRootedPath().ToString(), selected.MapName);
        else
        {
            // The lobby can request the catalogue before a map has been selected.
            foreach (var eligible in _maps.AllVotableMaps())
                mapPaths.TryAdd(eligible.MapPath.ToRootedPath().ToString(), eligible.MapName);
        }

        var entities = new List<GuidebookMappedEntity>();
        foreach (var (path, name) in mapPaths.OrderBy(p => p.Key))
        {
            if (!_mapLoader.TryReadFile(new ResPath(path), out var data))
            {
                Log.Error($"Guidebook catalogue could not read map {path}");
                continue;
            }

            result.MapNames[path] = name;
            if (sitePaths.TryGetValue(path, out var hidden)) result.Sites.Add(ReadSite(name, path, hidden, data));
            ReadMapEntities(data, path, name, entities);
            yield return true;
        }

        // Start from world objects and starting stations. A carrier's shipyard is
        // reachable only after its carrier can itself be obtained from those yards.
        var reachable = sitePaths.Keys.ToHashSet();
        foreach (var path in mapPaths.Keys.Where(p => !p.StartsWith("/SharedMaps/", StringComparison.Ordinal))) reachable.Add(path);
        var changed = true;
        while (changed)
        {
            changed = false;
            result.Shipyards = BuildShipyards(entities.Where(e => reachable.Contains(e.MapId)).ToList(), result.Vessels);
            var offered = result.Shipyards.SelectMany(y => y.Vessels).ToHashSet();
            foreach (var vessel in result.Vessels.Where(v => offered.Contains(v.Id))) changed |= reachable.Add(vessel.MapPath);
            yield return true;
        }
        var availableEntities = entities.Where(e => reachable.Contains(e.MapId)).ToList();
        var researchDisciplines = new HashSet<string>();
        foreach (var mapped in availableEntities)
        {
            if (_prototype.TryIndex<EntityPrototype>(mapped.PrototypeId, out var source)
                && source.Components.TryGetValue("TechnologyDatabase", out var component)
                && component.Component is TechnologyDatabaseComponent database)
                researchDisciplines.UnionWith(database.SupportedDisciplines.Select(d => d.Id));
            var configuration = mapped.Components?.OfType<MappingDataNode>().FirstOrDefault(c => Scalar(c, "type") == "TechnologyDatabase");
            if (configuration != null) researchDisciplines.UnionWith(Values(Node(configuration, "supportedDisciplines")));
        }
        result.ResearchDisciplines = researchDisciplines.Order().ToList();
        result.Equipment = BuildEquipment(availableEntities);
        yield return true;
        var obtainable = new HashSet<string>();
        result.Acquisitions = BuildAcquisitions(availableEntities, result.StoreListings, obtainable);
        yield return true;
        foreach (var step in BuildReferenceData(result, availableEntities, obtainable)) yield return step;
        foreach (var vessel in result.Vessels)
        foreach (var equipment in result.Equipment)
        {
            if (equipment.Locations.TryGetValue(vessel.MapPath, out var count))
                vessel.Equipment[equipment.Id] = count;
        }
        yield return true;
    }

    private bool VoucherAllows(ShipyardVoucherComponent voucher, VesselPrototype vessel)
    {
        if (voucher.ConsoleType != vessel.Group)
            return false;
        if (voucher.Vessels.Contains(vessel.ID))
            return true;
        if (!vessel.Purchasable || voucher.Access.Count == 0 && voucher.AccessGroups.Count == 0)
            return false;
        return string.IsNullOrEmpty(vessel.Access)
            || voucher.Access.Contains(vessel.Access)
            || voucher.AccessGroups.Any(g => _prototype.TryIndex(g, out var group) && group.Tags.Contains(vessel.Access));
    }

    private string LocalizedText(string key, string fallback)
    {
        if (!string.IsNullOrEmpty(key) && Loc.TryGetString(key, out var localized))
            return localized;
        if (fallback.StartsWith("vessel-", StringComparison.Ordinal) && Loc.TryGetString(fallback, out localized))
            return localized;
        return fallback;
    }

    private void ReadMapEntities(MappingDataNode data, string mapId, string mapName,
        List<GuidebookMappedEntity> entities)
    {
        foreach (var group in data.Get<SequenceDataNode>("entities").Cast<MappingDataNode>())
        {
            // Current map format groups serialized instances by their entity prototype.
            var id = group.Get<ValueDataNode>("proto").Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var equipmentPrototype = _prototype.TryIndex<EntityPrototype>(id, out var prototype)
                && (prototype.Components.ContainsKey("ShipGunType") || prototype.Components.ContainsKey("FireControlServer") || prototype.Components.ContainsKey("ShipyardConsole") || prototype.Components.Keys.Any(AcquisitionComponents.Contains));
            foreach (var instance in group.Get<SequenceDataNode>("entities").Cast<MappingDataNode>())
            {
                instance.TryGet("components", out SequenceDataNode? overrides);
                if (!equipmentPrototype && !(overrides?.OfType<MappingDataNode>().Any(c =>
                        c.TryGet("type", out ValueDataNode? type)
                        && (type.Value is "ShipGunType" or "FireControlServer" or "ShipyardConsole" or "ShipyardListing" || AcquisitionComponents.Contains(type.Value))) ?? false))
                    continue;
                entities.Add(new GuidebookMappedEntity(id, mapId, mapName, overrides));
            }
        }
    }
}
