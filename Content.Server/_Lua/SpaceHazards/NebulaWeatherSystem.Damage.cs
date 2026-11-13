// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.Radiation;
using Content.Server.Radiation.Components;
using Content.Server.Temperature.Components;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public sealed partial class NebulaWeatherSystem
{
    private const string HeatDamageType = "Heat";
    private const string ShockDamageType = "Shock";
    private const float HeatJoulesPerDamageUnit = 4000f;
    private const LookupFlags MobLookupFlags = LookupFlags.Dynamic | LookupFlags.Approximate | LookupFlags.Sundries;
    private static readonly TimeSpan ShockStun = TimeSpan.FromSeconds(2.5);

    // A mob in air at least this dense is inside a sealed hull, the weather doesn't reach it.
    private const float ShelterPressure = Atmospherics.OneAtmosphere * 0.2f;

    private readonly HashSet<Entity<MobStateComponent>> _mobScratch = new();

    private void ApplyHullDamageSample(EntityUid gridUid, NebulaWeatherPrototype weather)
    {
        if (weather.Damage.Empty || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var damaged = 0;
        var attempts = weather.MaxDamagedPerTick * 6;

        for (var i = 0; i < attempts && damaged < weather.MaxDamagedPerTick; i++)
        {
            if (!_random.Prob(weather.DamageChance))
                continue;

            if (GridHullExteriorHelper.TryPickRandomExteriorTile(gridUid, grid, _maps, _turf, _random, out var tile) &&
                TryDamageTile(gridUid, grid, tile, weather.Damage))
            {
                damaged++;
            }
        }
    }

    private bool TryDamageTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, DamageSpecifier damage)
    {
        var anyHit = false;
        var enumerator = _maps.GetAnchoredEntities(gridUid, grid, tile);
        while (enumerator.MoveNext(out var ent))
        {
            if (!TryComp(ent, out DamageableComponent? damageable))
                continue;

            var delta = _damageable.TryChangeDamage(
                ent,
                damage,
                ignoreResistances: true,
                interruptsDoAfters: false,
                damageable: damageable);

            if (delta != null && delta.GetTotal() > 0)
                anyHit = true;
        }

        return anyHit;
    }

    private void ApplyMobWeather(
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        float radius,
        NebulaWeatherPrototype weather,
        MapId mapId)
    {
        if (weather.Kind is NebulaWeatherKind.Veil or NebulaWeatherKind.EmpStorm)
            return;

        var mobDamage = weather.MobDamage.Empty ? weather.Damage : weather.MobDamage;
        if (mobDamage.Empty && weather.MobTemperatureIncrease <= 0f)
            return;

        // Heat and shock go through temperature and electrocution, the rest is dealt as is.
        var heatJoules = GetDamageAmount(mobDamage, HeatDamageType) * HeatJoulesPerDamageUnit;
        var shock = (int) MathF.Round(GetDamageAmount(mobDamage, ShockDamageType));
        var residual = WithoutPipelineDamage(mobDamage);

        _mobScratch.Clear();
        _lookup.GetEntitiesInRange(mapId, fieldPos, radius, _mobScratch, MobLookupFlags);

        foreach (var (uid, mobState) in _mobScratch)
        {
            if (mobState.CurrentState == MobState.Dead ||
                !NebulaVeilHelpers.IsInMidZone(field, fieldPos, _transform.GetWorldPosition(uid), radius) ||
                !IsMobExposedToNebula(uid))
                continue;

            if (weather.MobTemperatureIncrease > 0f)
            {
                if (TryComp(uid, out TemperatureComponent? temperature))
                {
                    var heat = _temperature.GetHeatCapacity(uid, temperature) * weather.MobTemperatureIncrease;
                    _temperature.ChangeHeat(uid, heat, temperature: temperature);
                }
            }
            else if (heatJoules > 0f)
            {
                _temperature.ChangeHeat(uid, heatJoules);
            }

            if (shock > 0)
                _electrocution.TryDoElectrocution(uid, null, shock, ShockStun, refresh: true);

            if (residual != null)
                _damageable.TryChangeDamage(uid, residual);
        }
    }

    private static float GetDamageAmount(DamageSpecifier damage, string typeId)
    {
        return damage.DamageDict.TryGetValue(typeId, out var amount) ? (float) amount : 0f;
    }

    private static DamageSpecifier? WithoutPipelineDamage(DamageSpecifier damage)
    {
        var copy = new DamageSpecifier(damage);
        copy.DamageDict.Remove(HeatDamageType);
        copy.DamageDict.Remove(ShockDamageType);
        foreach (var (key, value) in damage.DamageDict)
        {
            if (value <= FixedPoint2.Zero)
                copy.DamageDict.Remove(key);
        }

        return copy.Empty ? null : copy;
    }

    private bool IsMobExposedToNebula(EntityUid uid)
    {
        var air = _atmosphere.GetContainingMixture(uid);
        return air == null || air.Pressure < ShelterPressure;
    }

    private void OnGetAmbientRadiation(Entity<RadiationReceiverComponent> ent, ref GetAmbientRadiationEvent args)
    {
        if (_radiationFields.Count == 0)
            return;

        var xform = Transform(ent);
        if (xform.MapID == MapId.Nullspace)
            return;

        var position = _transform.GetWorldPosition(xform);
        var strongest = 0f;
        foreach (var fog in _radiationFields)
        {
            if (fog.Map != xform.MapID || !NebulaVeilHelpers.IsInMidZone(fog.Field, fog.Position, position))
                continue;

            var radius = MathF.Max(fog.Field.Radius, 1f);
            var depth = AmbientSpaceNebulaNoise.SamplePresence(
                (position - fog.Position) / radius,
                fog.Field.Seed,
                fog.Field.Density,
                radius);
            strongest = MathF.Max(strongest, fog.Intensity * Math.Clamp(depth, 0.25f, 1f));
        }

        if (strongest > 0f && IsMobExposedToNebula(ent))
            args.Radiation += strongest;
    }
}
