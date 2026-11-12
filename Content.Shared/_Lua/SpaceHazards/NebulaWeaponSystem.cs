// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.SpaceHazards;

/// <summary>
/// Slows down the guns on a grid caught in nebula weather. Shared so the shooter's client predicts the same rate.
/// </summary>
public sealed partial class NebulaWeaponSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, QueryFireRateMultiplierEvent>(OnFireRateQuery);
    }

    private void OnFireRateQuery(Entity<GunComponent> ent, ref QueryFireRateMultiplierEvent args)
    {
        if (Transform(ent).GridUid is not { } gridUid ||
            !TryComp(gridUid, out NebulaPresenceComponent? presence))
            return;

        var cooldownMultiplier = 1f;
        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (_prototypes.TryIndex(weatherId, out var weather))
                cooldownMultiplier = MathF.Max(cooldownMultiplier, weather.WeaponCooldownMultiplier);
        }

        if (cooldownMultiplier <= 1f)
            return;

        var resistance = TryComp(ent, out NebulaWeaponResistanceComponent? resist)
            ? Math.Clamp(resist.Resistance, 0f, 1f)
            : 0f;
        args.ReloadTimeMul *= float.Lerp(cooldownMultiplier, 1f, resistance);
    }
}
