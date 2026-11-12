// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.SpaceHazards;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.CCVar;
using Content.Shared._Lua.SpaceHazards;
using Robust.Server.GameStates;
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
    private const float RadiationFogMinRadius = 980f;
    private const float RadiationFogFullWeightRadius = 1500f;

    // The weather wakes up when a ship gets this close to the edge of the cloud.
    private const float ActivationMargin = 256f;

    private static readonly ProtoId<NebulaWeatherPrototype> RadiationFogWeather = "NebulaRadiationFog";

    // Indexed like AmbientSpacePalette.Colors: the weather a cloud of that color usually carries.
    private static readonly ProtoId<NebulaWeatherPrototype>[] WeatherByColorIndex =
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

    private bool _enabled = true;
    private readonly List<(Vector2 Pos, float Extent)> _gridScratch = new();

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceEffectsEnabled, v => _enabled = v, true);
        SubscribeLocalEvent<AmbientSpaceFieldPlacerControllerComponent, ComponentStartup>(OnPlacerStartup);
        SubscribeLocalEvent<AmbientSpaceFieldComponent, MapInitEvent>(OnFieldMapInit);
    }

    private void OnFieldMapInit(Entity<AmbientSpaceFieldComponent> ent, ref MapInitEvent args)
    {
        // A nebula is seen and drawn on the radar from far outside of the normal PVS range.
        _pvs.AddGlobalOverride(ent);

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
        TryPregenerate(uid, component);
    }

    private void TryPregenerate(EntityUid mapUid, AmbientSpaceFieldPlacerControllerComponent placer)
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

        var mapId = map.MapId;
        var count = _random.Next(placer.MinCount, placer.MaxCount + 1);
        var minDist = MathF.Max(0f, placer.MinDistance);
        var maxDist = MathF.Max(minDist + 1f, placer.MaxDistance);
        var minDistSq = minDist * minDist;
        var maxDistSq = maxDist * maxDist;
        var spawned = 0;
        var maxAttempts = Math.Max(count * 24, count + 256);
        var placed = new List<(Vector2 Pos, float Radius)>(count);
        CollectGrids(mapId, _gridScratch);

        for (var attempt = 0; attempt < maxAttempts && spawned < count; attempt++)
        {
            var seed = _random.Next();
            if (seed == 0)
                seed = 1;

            var radius = AmbientSpacePalette.RadiusFromSeed(seed);
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = MathF.Sqrt(minDistSq + _random.NextFloat() * (maxDistSq - minDistSq));
            var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (OverlapsPlaced(placed, pos, radius, placer.MinSpacingFactor, placer.MinCenterSeparation))
                continue;

            if (OverlapsGrid(_gridScratch, pos, radius + placer.MinGridClearance))
                continue;

            var ent = Spawn(placer.FieldPrototype, new MapCoordinates(pos, mapId));
            if (!TryComp<AmbientSpaceFieldComponent>(ent, out var field))
            {
                QueueDel(ent);
                continue;
            }

            var weatherChance = dist < placer.MinWeatherDistance ? 0f : placer.WeatherChance;
            ApplyDeterministicVisuals(field, seed, radius, weatherChance);
            Dirty(ent, field);
            UpdateActivationRange((ent, field));
            placed.Add((pos, radius));
            spawned++;
        }

        _gridScratch.Clear();
        Log.Info($"Pregenerated {spawned}/{count} ambient nebula fields on {ToPrettyString(mapUid)} (annulus {minDist}–{maxDist})");
    }

    private static bool OverlapsPlaced(
        List<(Vector2 Pos, float Radius)> placed,
        Vector2 pos,
        float radius,
        float spacingFactor,
        float minCenterSeparation)
    {
        var factor = Math.Clamp(spacingFactor, 0.05f, 2f);
        var floorSep = MathF.Max(0f, minCenterSeparation);

        foreach (var (otherPos, otherRadius) in placed)
        {
            var minDist = MathF.Max((radius + otherRadius) * factor, floorSep);
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

    private void ApplyDeterministicVisuals(AmbientSpaceFieldComponent field, int seed, float radius, float weatherChance)
    {
        field.Seed = seed;
        field.Density = AmbientSpacePalette.DensityFromSeed(seed);
        field.Radius = radius;
        field.Color = AmbientSpacePalette.ColorFromSeed(seed);
        field.Weathers.Clear();
        if (RollWeather(seed, radius, weatherChance) is not { } primaryWeather)
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

    private ProtoId<NebulaWeatherPrototype>? RollWeather(int seed, float radius, float weatherChance)
    {
        var u = unchecked((uint) seed);
        var weatherRoll = (u >> 12 & 0xFFFu) / 4095f;
        if (weatherRoll >= weatherChance)
            return null;

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
