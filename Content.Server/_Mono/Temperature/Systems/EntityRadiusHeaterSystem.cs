using Content.Server._Mono.Temperature.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.IgnitionSource;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared._LuaM.Performance; // LuaM
using Robust.Shared.Timing; // LuaM

namespace Content.Server._Mono.Temperature.Systems;

/// <summary>
/// Gives thermal energy to nearby entities.
/// </summary>
public sealed partial class EntityRadiusHeaterSystem : EntitySystem
{
    [Dependency] private TemperatureSystem _temp = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    [Dependency] private IGameTiming _timing = default!; // LuaM

    private float _updateCooldown = 1f;

    public override void Update(float frameTime)
    {
        // LuaM-start
        var period = TickSpread.Period(_timing, _updateCooldown);
        var tick = _timing.CurTick;
        // LuaM-end

        var eqe = EntityQueryEnumerator<EntityRadiusHeaterComponent>();

        while (eqe.MoveNext(out var uid, out var comp))
        {
            // LuaM-start
            if (!TickSpread.Due(uid, tick, period))
                continue;
            // LuaM-end

            if (comp.RequireActivation && TryComp<IgnitionSourceComponent>(uid, out var ignite)
                                       && !ignite.Ignited)
                continue;

            if (comp.RequireActivation && TryComp<ItemToggleComponent>(uid, out var toggle)
                                       && !toggle.Activated)
                continue;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            var nearby = _lookup.GetEntitiesInRange<TemperatureComponent>(Transform(uid).Coordinates, comp.Radius, LookupFlags.Dynamic | LookupFlags.Sundries); // LuaM
            var xform = Transform(uid);
            foreach (var ent in nearby)
            {
                _temp.ChangeHeat(ent, CalculateThermalEnergy(ent, xform, comp));
            }
        }
    }

    public float CalculateThermalEnergy(Entity<TemperatureComponent> ent,
        TransformComponent heaterXform,
        EntityRadiusHeaterComponent comp)
    {
        if (!Transform(ent).Coordinates.TryDistance(EntityManager, heaterXform.Coordinates, out var distance))
            return 0f;

        var c = 1 - (distance / comp.Radius);

        if (c < 0)
            return 0;

        var oT = _temp.GetHeatCapacity(ent) * ent.Comp.CurrentTemperature;
        var nT = Math.Clamp(c * comp.ThermalEnergy + oT, 0, _temp.GetHeatCapacity(ent) * comp.Limit);

        var d = nT - oT;

        if (d < 0)
            return 0;

        return d;
    }
}
