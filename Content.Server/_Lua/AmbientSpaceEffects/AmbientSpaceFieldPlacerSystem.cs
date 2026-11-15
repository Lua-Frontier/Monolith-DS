// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.SpaceHazards;
using Content.Server._NF.GameTicking.Events;
using Content.Server.GameTicking;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.CCVar;
using Content.Shared._Lua.SpaceHazards;
using Robust.Server.GameStates;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.AmbientSpaceEffects;

/// <summary>
/// Scatters nebulae over a map that got <see cref="AmbientSpaceFieldPlacerControllerComponent"/>
/// and sets up every nebula that appears, placed by it or by hand.
/// </summary>
public sealed partial class AmbientSpaceFieldPlacerSystem : EntitySystem
{
    private const float WeatherOffPaletteChance = 0.18f;
    internal const float RadiationFogMinRadius = 980f; // LuaM: private > internal, NebulaShiftRuleSystem
    private const float RadiationFogFullWeightRadius = 1500f;

    // The weather wakes up when a ship gets this close to the edge of the cloud.
    internal const float ActivationMargin = 256f; // LuaM: private > internal

    internal static readonly ProtoId<NebulaWeatherPrototype> RadiationFogWeather = "NebulaRadiationFog"; // LuaM: private > internal

    // Indexed like AmbientSpacePalette.Colors: the weather a cloud of that color usually carries.
    internal static readonly ProtoId<NebulaWeatherPrototype>[] WeatherByColorIndex = // LuaM: private > internal
    [
        "NebulaLightning",
        "NebulaCorrosion",
        "NebulaEmpStorm",
        "NebulaVeil",
        "NebulaRadiationFog",
        "NebulaHeatWash",
        "NebulaHeatWash",
    ];

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpaceHazardActivitySystem _activity = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private GameTicker _ticker = default!;

    private bool _enabled = true;
    private readonly List<(Vector2 Pos, float Extent)> _gridScratch = new();

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceEffectsEnabled, v => _enabled = v, true);
        SubscribeLocalEvent<AmbientSpaceFieldPlacerControllerComponent, ComponentStartup>(OnPlacerStartup);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, MapInitEvent>(OnFieldMapInit);
        SubscribeLocalEvent<StationsGeneratedEvent>(_ => GenerateAll());
        SubscribeLocalEvent<RoundStartedEvent>(_ => GenerateAll());
    }

    private void OnFieldMapInit(Entity<AmbientSpaceFieldComponent> ent, ref MapInitEvent args)
    {
        // A nebula is seen and drawn on the radar from far outside of the normal PVS range.
        _pvs.AddGlobalOverride(ent);

        var xform = Transform(ent);
        if (xform.MapUid is { } mapUid && xform.ParentUid != mapUid)
            _transform.SetCoordinates(ent, xform, new EntityCoordinates(mapUid, _transform.GetWorldPosition(xform)));

        // Spawned by hand or mapped, it has no shape yet.
        if (ent.Comp.Seed == 0)
        {
            ent.Comp.Seed = _random.Next(1, int.MaxValue);
            Dirty(ent);
        }

        UpdateActivationRange(ent);
    }

    private void UpdateActivationRange(Entity<AmbientSpaceFieldComponent> ent)
    {
        if (ent.Comp.HasWeather)
            _activity.SetActivationRange(ent.Owner, ent.Comp.Radius + ActivationMargin);
    }

    private void OnPlacerStartup(EntityUid uid, AmbientSpaceFieldPlacerControllerComponent component, ComponentStartup args)
    {
        if (_ticker.RunLevel == GameRunLevel.InRound)
            Generate(uid, component);
    }

    private void GenerateAll()
    {
        var query = EntityQueryEnumerator<AmbientSpaceFieldPlacerControllerComponent>();
        while (query.MoveNext(out var uid, out var placer))
        {
            Generate(uid, placer);
        }
    }

    private void Generate(EntityUid mapUid, AmbientSpaceFieldPlacerControllerComponent placer)
    {
        if (!_enabled || placer.Spawned)
            return;

        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        placer.Spawned = true;

        if (!_prototypes.HasIndex<EntityPrototype>(placer.FieldPrototype))
        {
            Log.Error($"Ambient space field prototype '{placer.FieldPrototype}' missing");
            return;
        }

        var count = _random.Next(placer.MinCount, placer.MaxCount + 1);
        var minDist = MathF.Max(0f, placer.MinDistance);
        var maxDist = MathF.Max(minDist + 1f, placer.MaxDistance);
        var maxDistSq = maxDist * maxDist;
        var dangerous = 0;
        var maxAttempts = Math.Max(count * 24, count + 256);
        var placed = new List<(Vector2 Pos, float Radius)>(count);
        CollectGrids(map.MapId, _gridScratch);

        for (var attempt = 0; attempt < maxAttempts && placed.Count < count; attempt++)
        {
            var seed = _random.Next();
            if (seed == 0)
                seed = 1;

            var radius = AmbientSpacePalette.RadiusFromSeed(seed);
            var minCenterDist = minDist + radius;
            if (minCenterDist >= maxDist)
                continue;

            var angle = _random.NextFloat() * MathF.Tau;
            var minCenterDistSq = minCenterDist * minCenterDist;
            var dist = MathF.Sqrt(minCenterDistSq + _random.NextFloat() * (maxDistSq - minCenterDistSq));
            var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (OverlapsPlaced(placed, pos, radius, placer.Spacing))
                continue;

            var ent = Spawn(placer.FieldPrototype, new EntityCoordinates(mapUid, pos));
            if (!TryComp<AmbientSpaceFieldComponent>(ent, out var field))
            {
                QueueDel(ent);
                continue;
            }

            var hasWeather = !OverlapsGrid(_gridScratch, pos, radius + placer.DangerClearance) &&
                             _random.Prob(placer.DangerChance);
            ApplyDeterministicVisuals(field, seed, radius, hasWeather);
            Dirty(ent, field);
            UpdateActivationRange((ent, field));
            placed.Add((pos, radius));
            if (field.HasWeather)
                dangerous++;
        }

        _gridScratch.Clear();
        Log.Info($"Generated {placed.Count}/{count} nebulae on {ToPrettyString(mapUid)}, {dangerous} with weather");
    }

    private static bool OverlapsPlaced(List<(Vector2 Pos, float Radius)> placed, Vector2 pos, float radius, float spacing)
    {
        foreach (var (otherPos, otherRadius) in placed)
        {
            var minDist = radius + otherRadius + spacing;
            if ((pos - otherPos).LengthSquared() < minDist * minDist)
                return true;
        }

        return false;
    }

    private void CollectGrids(MapId mapId, List<(Vector2 Pos, float Extent)> output)
    {
        output.Clear();
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out _, out var grid, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var aabb = grid.LocalAABB;
            var center = Vector2.Transform(aabb.Center, _transform.GetWorldMatrix(xform));
            output.Add((center, aabb.Size.Length() * 0.5f));
        }
    }

    private static bool OverlapsGrid(List<(Vector2 Pos, float Extent)> grids, Vector2 pos, float clearance)
    {
        foreach (var (gridPos, extent) in grids)
        {
            var minDist = clearance + extent;
            if ((pos - gridPos).LengthSquared() < minDist * minDist)
                return true;
        }

        return false;
    }

    private void ApplyDeterministicVisuals(AmbientSpaceFieldComponent field, int seed, float radius, bool hasWeather)
    {
        field.Seed = seed;
        field.Density = AmbientSpacePalette.DensityFromSeed(seed);
        field.Radius = radius;
        field.Color = AmbientSpacePalette.ColorFromSeed(seed);
        field.Weathers.Clear();
        if (!hasWeather || RollWeather(seed, radius) is not { } primaryWeather)
            return;

        field.Weathers.Add(primaryWeather);
        var colorIdx = AmbientSpacePalette.ColorIndexFromSeed(seed);
        var colorWeather = WeatherByColorIndex[Math.Clamp(colorIdx, 0, WeatherByColorIndex.Length - 1)];
        if (colorWeather != primaryWeather && CanAddSecondaryWeather(colorWeather, seed, radius))
            field.Weathers.Add(colorWeather);
    }

    private bool CanAddSecondaryWeather(ProtoId<NebulaWeatherPrototype> weatherId, int seed, float radius)
    {
        if (!_prototypes.HasIndex(weatherId))
            return false;

        if (weatherId != RadiationFogWeather)
            return true;

        var radiationRoll = (unchecked((uint) seed) >> 24 & 0xFFu) / 255f;
        var radiationWeight = RadiationWeight(radius);
        return radiationWeight > 0f && radiationRoll < radiationWeight;
    }

    // Radiation fog only fills big clouds.
    private static float RadiationWeight(float radius)
    {
        return Math.Clamp(
            (radius - RadiationFogMinRadius) / (RadiationFogFullWeightRadius - RadiationFogMinRadius),
            0f,
            1f);
    }

    private ProtoId<NebulaWeatherPrototype>? RollWeather(int seed, float radius)
    {
        var u = unchecked((uint) seed);
        var radiationWeight = RadiationWeight(radius);
        var radiationRoll = (u >> 24 & 0xFFu) / 255f;
        if (radiationWeight > 0f && radiationRoll < radiationWeight * 0.35f && _prototypes.HasIndex(RadiationFogWeather))
            return RadiationFogWeather;

        var colorIdx = AmbientSpacePalette.ColorIndexFromSeed(seed);
        var preferred = WeatherByColorIndex[Math.Clamp(colorIdx, 0, WeatherByColorIndex.Length - 1)];
        if (preferred == RadiationFogWeather && radiationRoll >= radiationWeight)
            preferred = WeatherByColorIndex[(colorIdx + 1) % WeatherByColorIndex.Length];

        var offPalette = ((u >> 4) & 0xFFu) / 255f < WeatherOffPaletteChance;
        if (!offPalette)
            return _prototypes.HasIndex(preferred) ? preferred : (ProtoId<NebulaWeatherPrototype>?) null;

        var pickIdx = (int) ((u >> 8) % (uint) WeatherByColorIndex.Length);
        var alt = WeatherByColorIndex[pickIdx];
        if (alt == RadiationFogWeather && radiationRoll >= radiationWeight)
            alt = preferred;

        if (_prototypes.HasIndex(alt))
            return alt;

        if (_prototypes.HasIndex(preferred))
            return preferred;

        return null;
    }
}
