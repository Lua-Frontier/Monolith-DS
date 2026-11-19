/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Water;

public sealed class LuaMWaterSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> CatwalkTag = "Catwalk";

    private EntityQuery<LuaMWaterComponent> _waterQuery;

    public override void Initialize()
    {
        base.Initialize();

        _waterQuery = GetEntityQuery<LuaMWaterComponent>();
    }

    // -- Берётся тайл под центром сущности: краем хитбокса в воду не погружаются. Мостки накрывают воду
    public bool TryGetWater(EntityUid uid, [NotNullWhen(true)] out Entity<LuaMWaterComponent>? water, out bool covered, TransformComponent? xform = null)
    {
        water = null;
        covered = false;
        if (!Resolve(uid, ref xform, false) ||
            xform.GridUid is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return false;
        }

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_waterQuery.TryComp(ent, out var comp))
            {
                if (water == null || comp.Depth > water.Value.Comp.Depth)
                    water = (ent.Value, comp);
            }
            else if (_tags.HasTag(ent.Value, CatwalkTag))
            {
                covered = true;
            }
        }

        return water != null;
    }

    public bool CanSubmerge(EntityUid uid)
    {
        return !_container.IsEntityInContainer(uid) &&
               !(TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled) &&
               !(TryComp<ThrownItemComponent>(uid, out var thrown) && !thrown.Landed);
    }

    public float GetVehicleSpeedFactor(EntityUid vehicle, VehicleWeightClass weight)
    {
        if (!TryGetWater(vehicle, out var water, out var covered) || covered ||
            !water.Value.Comp.VehicleSpeedFactors.TryGetValue(weight, out var factor))
        {
            return 1f;
        }

        return Math.Clamp(factor, 0f, 1f);
    }
}
