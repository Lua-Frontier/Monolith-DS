// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Server._Lua.Radiation;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Electrocution;
using Content.Server.Emp;
using Content.Server.Lightning;
using Content.Server.Radiation.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Lua.SpaceHazards;

/// <summary>
/// Applies the weather of a nebula to the grids and mobs inside its dense part.
/// </summary>
public sealed partial class NebulaWeatherSystem : EntitySystem
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly EntProtoId LightningBoltProto = "Lightning";
    private static readonly EntProtoId LightningSparkProto = "Spark";
    private static readonly EntProtoId AcidEffectProto = "Acidifier";
    private static readonly EntProtoId RadiationEffectProto = "NebulaRadiationPulse";
    private static readonly EntProtoId SparksEffectProto = "EffectSparks";
    private static readonly EntProtoId EmpPulseSpriteProto = "EffectEmpPulse";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpaceHazardActivitySystem _activity = default!;
    [Dependency] private TemperatureSystem _temperature = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    private TimeSpan _nextTick;
    private readonly List<EntityUid> _gridScratch = new();
    private List<Entity<MapGridComponent>> _gridQueryScratch = new();
    private readonly HashSet<EntityUid> _veiledThisTick = new();
    private readonly Dictionary<EntityUid, Dictionary<ProtoId<NebulaWeatherPrototype>, float>> _weatherSnapshots = new();
    private readonly List<EntityUid> _activeScratch = new();
    private readonly List<RadiationField> _radiationFields = new();

    // Keyed by the grid or, for mob weather, by the nebula itself.
    private readonly Dictionary<(EntityUid Target, string Weather), TimeSpan> _nextWeatherEvents = new();
    private readonly HashSet<(EntityUid Target, string Weather)> _weatherEventsSeenThisTick = new();

    private readonly record struct RadiationField(
        MapId Map,
        Vector2 Position,
        AmbientSpaceFieldComponent Field,
        float Intensity);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadiationReceiverComponent, GetAmbientRadiationEvent>(OnGetAmbientRadiation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        _veiledThisTick.Clear();
        _weatherSnapshots.Clear();
        _weatherEventsSeenThisTick.Clear();
        _radiationFields.Clear();
        _activeScratch.Clear();
        _activeScratch.AddRange(_activity.ActiveHazards);

        foreach (var uid in _activeScratch)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field) || !field.HasWeather)
                continue;

            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
                continue;

            var fieldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            CollectAffectedGrids(xform.MapID, field, fieldPos, radius, _gridScratch);

            foreach (var weatherId in field.Weathers)
            {
                if (!_prototypes.TryIndex(weatherId, out var weather))
                    continue;

                if (weather.Kind == NebulaWeatherKind.RadiationFog && weather.RadiationIntensity > 0f)
                    _radiationFields.Add(new RadiationField(xform.MapID, fieldPos, field, weather.RadiationIntensity));

                foreach (var gridUid in _gridScratch)
                {
                    RecordPresence(gridUid, field, fieldPos, radius, weatherId);
                    ApplyWeather(gridUid, weatherId, weather, xform.MapID, now);
                }

                if (IsWeatherEventDue(uid, weatherId, weather, now))
                    ApplyMobWeather(field, fieldPos, radius, weather, xform.MapID);
            }
        }

        CommitPresence();
        CleanupVeil();
        CleanupWeatherEventTimers();
    }

    private void RecordPresence(
        EntityUid gridUid,
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        float radius,
        ProtoId<NebulaWeatherPrototype> weatherId)
    {
        var samplePos = TryComp<MapGridComponent>(gridUid, out var grid)
            ? ClosestPointOnGrid(gridUid, grid, fieldPos)
            : _transform.GetWorldPosition(gridUid);
        var normalized = (samplePos - fieldPos) / radius;
        var intensity = AmbientSpaceNebulaNoise.SamplePresence(normalized, field.Seed, field.Density, radius);

        if (!_weatherSnapshots.TryGetValue(gridUid, out var snapshot))
        {
            snapshot = new Dictionary<ProtoId<NebulaWeatherPrototype>, float>();
            _weatherSnapshots.Add(gridUid, snapshot);
        }

        if (!snapshot.TryGetValue(weatherId, out var existingIntensity) || intensity > existingIntensity)
            snapshot[weatherId] = intensity;
    }

    private void CommitPresence()
    {
        foreach (var (gridUid, snapshot) in _weatherSnapshots)
        {
            var presence = EnsureComp<NebulaPresenceComponent>(gridUid);
            if (SamePresence(presence, snapshot))
                continue;

            presence.ActiveWeathers.Clear();
            presence.ActiveIntensities.Clear();
            foreach (var (weatherId, intensity) in snapshot)
            {
                presence.ActiveWeathers.Add(weatherId);
                presence.ActiveIntensities.Add(intensity);
            }

            Dirty(gridUid, presence);
        }

        var query = EntityQueryEnumerator<NebulaPresenceComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_weatherSnapshots.ContainsKey(uid))
                RemCompDeferred<NebulaPresenceComponent>(uid);
        }
    }

    private static bool SamePresence(
        NebulaPresenceComponent presence,
        Dictionary<ProtoId<NebulaWeatherPrototype>, float> snapshot)
    {
        if (presence.ActiveWeathers.Count != snapshot.Count)
            return false;

        for (var i = 0; i < presence.ActiveWeathers.Count; i++)
        {
            if (!snapshot.TryGetValue(presence.ActiveWeathers[i], out var intensity) ||
                MathF.Abs(presence.ActiveIntensities[i] - intensity) >= 0.01f)
                return false;
        }

        return true;
    }

    private void CollectAffectedGrids(
        MapId mapId,
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        float radius,
        List<EntityUid> output)
    {
        output.Clear();
        _gridQueryScratch.Clear();
        var box = Box2.CenteredAround(fieldPos, new Vector2(radius * 2f, radius * 2f));
        _maps.FindGridsIntersecting(mapId, box, ref _gridQueryScratch, approx: true, includeMap: false);
        foreach (var grid in _gridQueryScratch)
        {
            var samplePos = ClosestPointOnGrid(grid.Owner, grid.Comp, fieldPos);
            if (NebulaVeilHelpers.IsInMidZone(field, fieldPos, samplePos, radius))
                output.Add(grid.Owner);
        }
    }

    private Vector2 ClosestPointOnGrid(EntityUid gridUid, MapGridComponent grid, Vector2 worldPoint)
    {
        var local = Vector2.Transform(worldPoint, _transform.GetInvWorldMatrix(gridUid));
        var aabb = grid.LocalAABB;
        var closest = Vector2.Clamp(local, aabb.BottomLeft, aabb.TopRight);
        return Vector2.Transform(closest, _transform.GetWorldMatrix(gridUid));
    }

    private void ApplyWeather(
        EntityUid gridUid,
        ProtoId<NebulaWeatherPrototype> weatherId,
        NebulaWeatherPrototype weather,
        MapId mapId,
        TimeSpan now)
    {
        if (weather.Kind == NebulaWeatherKind.Veil)
        {
            _veiledThisTick.Add(gridUid);
            EnsureComp<NebulaVeilTrackedComponent>(gridUid);
            return;
        }

        if (!IsWeatherEventDue(gridUid, weatherId, weather, now))
            return;

        if (weather.Kind == NebulaWeatherKind.EmpStorm)
        {
            EmpOnHull(gridUid, weather, mapId);
            MaybeSpawnHullEffect(gridUid, mapId, EmpPulseSpriteProto, chance: 0.5f, maxSpawns: 1);
            return;
        }

        if (TryAbsorbGridHazard(gridUid, weather))
            return;

        ApplyHullDamageSample(gridUid, weather);

        switch (weather.Kind)
        {
            case NebulaWeatherKind.Lightning:
                if (_random.Prob(weather.EmpChance))
                    EmpOnHull(gridUid, weather, mapId);
                SpawnLightningArcs(gridUid, mapId);
                break;
            case NebulaWeatherKind.Corrosion:
                MaybeSpawnHullEffect(gridUid, mapId, AcidEffectProto, chance: 0.65f, maxSpawns: 3);
                break;
            case NebulaWeatherKind.RadiationFog:
                MaybeSpawnHullEffect(gridUid, mapId, RadiationEffectProto, chance: 0.55f, maxSpawns: 2);
                break;
            case NebulaWeatherKind.HeatWash:
                SpawnHeatFlashes(gridUid, mapId);
                MaybeSpawnHullEffect(gridUid, mapId, SparksEffectProto, chance: 0.5f, maxSpawns: 2);
                break;
        }
    }

    /// <summary>
    /// Whether the next weather event for this target has come, and if so, schedules the one after it.
    /// The first call only starts the timer, so nothing hits the moment a ship enters the cloud.
    /// </summary>
    private bool IsWeatherEventDue(
        EntityUid target,
        ProtoId<NebulaWeatherPrototype> weatherId,
        NebulaWeatherPrototype weather,
        TimeSpan now)
    {
        var key = (target, weatherId.Id);
        _weatherEventsSeenThisTick.Add(key);

        var known = _nextWeatherEvents.TryGetValue(key, out var nextEvent);
        if (known && now < nextEvent)
            return false;

        var minDelay = Math.Max(1, Math.Min(weather.MinEventDelaySeconds, weather.MaxEventDelaySeconds));
        var maxDelay = Math.Max(minDelay, Math.Max(weather.MinEventDelaySeconds, weather.MaxEventDelaySeconds));
        _nextWeatherEvents[key] = now + TimeSpan.FromSeconds(_random.Next(minDelay, maxDelay + 1));
        return known;
    }

    private void CleanupWeatherEventTimers()
    {
        if (_nextWeatherEvents.Count == _weatherEventsSeenThisTick.Count)
            return;

        foreach (var key in _nextWeatherEvents.Keys.ToArray())
        {
            if (!_weatherEventsSeenThisTick.Contains(key))
                _nextWeatherEvents.Remove(key);
        }
    }

    /// <summary>
    /// A raised ship shield takes the hit instead of the hull.
    /// </summary>
    private bool TryAbsorbGridHazard(EntityUid gridUid, NebulaWeatherPrototype weather)
    {
        if (weather.ShieldLoad <= 0f)
            return false;

        var attempt = new NebulaShieldHitAttemptEvent(weather.ShieldLoad);
        RaiseLocalEvent(gridUid, ref attempt);
        return attempt.Absorbed;
    }

    private void CleanupVeil()
    {
        var query = EntityQueryEnumerator<NebulaVeilTrackedComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_veiledThisTick.Contains(uid))
                RemCompDeferred<NebulaVeilTrackedComponent>(uid);
        }
    }

    private void EmpOnHull(EntityUid gridUid, NebulaWeatherPrototype weather, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var pulses = Math.Max(weather.EmpPulsesPerTick, 1);
        for (var i = 0; i < pulses; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile))
                break;

            var coords = new MapCoordinates(_maps.GridTileToWorldPos(gridUid, grid, tile), mapId);
            _emp.EmpPulse(coords, weather.EmpRange, weather.EmpEnergy, TimeSpan.FromSeconds(weather.EmpDuration));
            Spawn(SparksEffectProto, coords);
        }
    }

    private void SpawnLightningArcs(EntityUid gridUid, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var bolts = _random.Next(1, 3);
        for (var i = 0; i < bolts; i++)
        {
            if (!TryPickDistantExteriorPair(gridUid, grid, out var tileA, out var tileB))
                break;

            var coordsA = new MapCoordinates(_maps.GridTileToWorldPos(gridUid, grid, tileA), mapId);
            var coordsB = new MapCoordinates(_maps.GridTileToWorldPos(gridUid, grid, tileB), mapId);
            var source = SpawnTempAnchor(coordsA);
            var target = SpawnTempAnchor(coordsB);

            var proto = _random.Prob(0.35f) ? LightningSparkProto : LightningBoltProto;
            _lightning.ShootLightning(source, target, proto, triggerLightningEvents: false);
            if (_random.Prob(0.25f))
            {
                _lightning.ShootRandomLightnings(
                    source,
                    range: 8f,
                    boltCount: 1,
                    lightningPrototype: LightningBoltProto,
                    arcDepth: 1,
                    triggerLightningEvents: false);
            }

            Spawn(SparksEffectProto, coordsA);
            Spawn(SparksEffectProto, coordsB);
        }
    }

    private void SpawnHeatFlashes(EntityUid gridUid, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var flashes = _random.Next(1, 4);
        for (var i = 0; i < flashes; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile))
                break;

            var flash = Spawn(null, new MapCoordinates(_maps.GridTileToWorldPos(gridUid, grid, tile), mapId));
            var light = EnsureComp<PointLightComponent>(flash);
            _pointLight.SetColor(flash, Color.FromHex("#FF6A20"), light);
            _pointLight.SetRadius(flash, 7f, light);
            _pointLight.SetEnergy(flash, 10f, light);
            _pointLight.SetCastShadows(flash, false, light);
            EnsureComp<TimedDespawnComponent>(flash).Lifetime = 0.4f;
        }
    }

    private void MaybeSpawnHullEffect(
        EntityUid gridUid,
        MapId mapId,
        EntProtoId effectProto,
        float chance,
        int maxSpawns)
    {
        if (!_random.Prob(chance) || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        for (var i = 0; i < maxSpawns; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile))
                break;

            Spawn(effectProto, new MapCoordinates(_maps.GridTileToWorldPos(gridUid, grid, tile), mapId));
        }
    }

    private bool TryPickDistantExteriorPair(
        EntityUid gridUid,
        MapGridComponent grid,
        out Vector2i tileA,
        out Vector2i tileB)
    {
        tileA = default;
        tileB = default;

        for (var i = 0; i < 24; i++)
        {
            if (!GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out tileA) ||
                !GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out tileB))
                return false;

            if ((tileA - tileB).LengthSquared >= 4)
                return true;
        }

        return false;
    }

    private EntityUid SpawnTempAnchor(MapCoordinates coords)
    {
        var uid = Spawn(null, coords);
        EnsureComp<TimedDespawnComponent>(uid).Lifetime = 0.6f;
        return uid;
    }
}
