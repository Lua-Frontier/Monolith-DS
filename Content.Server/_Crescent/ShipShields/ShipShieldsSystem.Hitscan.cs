using Content.Server._Crescent.ShipShields.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;

namespace Content.Server._Crescent.ShipShields;

// LuaM: ship shields block ship lasers, so the lasers have to wear the shield down like projectiles do.
public partial class ShipShieldsSystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    private void InitializeHitscan()
    {
        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanRaycastFiredEvent>(OnHitscanFired, after: [ typeof(HitscanReflectSystem) ]);
    }

    /// <summary>
    /// A shield has no damageable, so the normal hitscan damage does nothing to it.
    /// The damage goes to the emitter instead, the same way a deflected projectile does.
    /// </summary>
    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!TryComp<ShipShieldComponent>(hit, out var shield)
                || shield.Source is not { } source
                || !TryComp<ShipShieldEmitterComponent>(source, out var emitter))
                continue;

            if (TryComp<HitscanBasicDamageComponent>(ent, out var damage))
                emitter.Damage += (float) (damage.Damage * _damageable.UniversalHitscanDamageModifier).GetTotal();

            // The explosion of the laser is not spawned on a shield (it would go off inside the ship), so the shield takes it.
            if (TryComp<HitscanSpawnEntityComponent>(ent, out var spawn)
                && _prototypeManager.TryIndex(spawn.SpawnedEntity, out var spawnedProto)
                && spawnedProto.TryComp<ExplosiveComponent>(out var explosive, Factory)
                && _prototypeManager.TryIndex(explosive.ExplosionType, out var explosionType))
            {
                emitter.Damage += explosive.TotalIntensity * (float) explosionType.DamagePerIntensity.GetTotal();
            }
        }
    }
}
