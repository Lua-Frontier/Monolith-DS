// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._White.Standing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Goobstation.Shared.Grab;

/// <summary>
/// Throws mobs so they knock down and hurt the ones they fly into.
/// </summary>
public sealed class GrabThrownSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedLayingDownSystem _layingDown = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    /// <summary>
    /// Kinetic energy that gives one point of blunt damage to the one hit.
    /// </summary>
    private const float EnergyPerDamage = 300f;

    /// <summary>
    /// Kinetic energy that gives one point of stamina damage to the thrown one.
    /// </summary>
    private const float EnergyPerStamina = 200f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrabThrownComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<GrabThrownComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnCollide(Entity<GrabThrownComponent> ent, ref StartCollideEvent args)
    {
        // Effects are only done on the server to avoid them twice
        if (_net.IsClient)
            return;

        var other = args.OtherEntity;
        if (!HasComp<ThrownItemComponent>(ent))
        {
            RemCompDeferred<GrabThrownComponent>(ent);
            return;
        }

        if (!ent.Comp.IgnoreEntity.Add(other) || !args.OtherFixture.Hard)
            return;

        var kineticEnergy = 0.5f * args.OurBody.Mass * args.OurBody.LinearVelocity.LengthSquared();

        if (HasComp<DamageableComponent>(other))
        {
            var damage = MathF.Floor(kineticEnergy / EnergyPerDamage);
            if (damage > 0)
            {
                var spec = new DamageSpecifier();
                spec.DamageDict.Add("Blunt", damage);
                _damageable.TryChangeDamage(other, spec, origin: ent);
            }
        }

        _stamina.TakeStaminaDamage(ent, MathF.Floor(kineticEnergy / EnergyPerStamina), visual: false);

        // Bowling
        if (HasComp<MobStateComponent>(other))
            _layingDown.TryLieDown(other, behavior: DropHeldItemsBehavior.NoDrop);

        _color.RaiseEffect(Color.Red, new List<EntityUid> { ent }, Filter.Pvs(ent, entityManager: EntityManager));
    }

    private void OnStopThrow(Entity<GrabThrownComponent> ent, ref StopThrowEvent args)
    {
        if (_net.IsServer && ent.Comp.DamageOnLand is { } damage)
            _damageable.TryChangeDamage(ent, damage);

        RemCompDeferred<GrabThrownComponent>(ent);
    }

    /// <summary>
    /// Throws the entity to the direction, it lands lying on the floor.
    /// The length of <paramref name="vector"/> is the throw distance.
    /// </summary>
    public void Throw(EntityUid uid, EntityUid thrower, Vector2 vector, float speed, DamageSpecifier? damageOnLand = null)
    {
        if (vector.LengthSquared() < 0.0001f)
            return;

        var comp = EnsureComp<GrabThrownComponent>(uid);
        comp.IgnoreEntity.Add(thrower);
        comp.DamageOnLand = damageOnLand;

        _layingDown.TryLieDown(uid, behavior: DropHeldItemsBehavior.NoDrop);
        _throwing.TryThrow(uid, vector, speed, recoil: false, animated: false, doSpin: false);
    }
}
