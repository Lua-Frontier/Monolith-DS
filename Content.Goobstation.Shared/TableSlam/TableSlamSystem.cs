// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.GrabIntent;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Actions.Events;
using Content.Shared.Climbing.Components;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.TableSlam;

/// <summary>
/// Hitting a table while holding someone in a hard grab slams them onto it.
/// </summary>
public sealed class TableSlamSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrabIntentComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<GrabbableComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<GrabbableComponent, StopThrowEvent>(OnStopThrow);
        SubscribeLocalEvent<PostTabledComponent, DisarmAttemptEvent>(OnDisarmAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<PostTabledComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.EndTime)
                RemCompDeferred<PostTabledComponent>(uid);
        }
    }

    /// <summary>
    /// The component is on the user, so only unarmed precise hits get here.
    /// </summary>
    private void OnMeleeHit(Entity<GrabIntentComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Direction != null
            || args.HitEntities.Count != 1
            || ent.Comp.GrabStage < ent.Comp.TableSlamRequiredStage
            || !TryComp<PullerComponent>(ent, out var puller)
            || puller.Pulling is not { } target
            || !HasComp<BonkableComponent>(args.HitEntities[0]))
            return;

        var table = args.HitEntities[0];

        // The heavier the one slammed, the harder it is
        var chance = 1f;
        if (TryComp<PhysicsComponent>(ent, out var pullerPhysics)
            && TryComp<PhysicsComponent>(target, out var targetPhysics)
            && targetPhysics.Mass > 0f)
            chance = Math.Clamp(pullerPhysics.Mass / targetPhysics.Mass, 0f, 1f);

        var random = new System.Random(SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value, GetNetEntity(ent).Id));
        if (random.NextDouble() < chance)
            TryTableSlam(target, ent, table);
    }

    public void TryTableSlam(EntityUid target, Entity<GrabIntentComponent> puller, EntityUid table)
    {
        if (!TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<GrabbableComponent>(target, out var grabbable)
            || !_transform.InRange(target.ToCoordinates(), table.ToCoordinates(), puller.Comp.TableSlamRange))
            return;

        if (TryComp<MeleeWeaponComponent>(puller, out var melee))
        {
            melee.NextAttack = _timing.CurTime + puller.Comp.TableSlamCooldown;
            Dirty(puller.Owner, melee);
        }

        _standing.Down(target);
        _pulling.TryStopPull(target, pullable, puller);
        _throwing.TryThrow(target, table.ToCoordinates(), grabbable.TabledThrowSpeed, animated: false, doSpin: false);

        grabbable.BeingTabled = true;
        Dirty(target, grabbable);
    }

    private void OnStartCollide(Entity<GrabbableComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.BeingTabled || !HasComp<BonkableComponent>(args.OtherEntity))
            return;

        ent.Comp.BeingTabled = false;
        Dirty(ent);

        if (_net.IsClient)
            return;

        var stunDuration = ent.Comp.PostTabledDuration;

        // A glass table breaks and cuts
        if (TryComp<GlassTableComponent>(args.OtherEntity, out var glass))
        {
            _damageable.TryChangeDamage(args.OtherEntity, glass.TableDamage, origin: ent);
            _damageable.TryChangeDamage(ent, glass.ClimberDamage, origin: ent);
            stunDuration *= 2;
        }
        else
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", ent.Comp.TabledDamage);
            _damageable.TryChangeDamage(ent, damage, targetPart: TargetBodyPart.Torso);
        }

        _stamina.TakeStaminaDamage(ent, ent.Comp.TabledStaminaDamage);
        _stun.TryKnockdown(ent, stunDuration, true);

        var postTabled = EnsureComp<PostTabledComponent>(ent);
        postTabled.EndTime = _timing.CurTime + ent.Comp.PostTabledDuration;
    }

    private void OnStopThrow(Entity<GrabbableComponent> ent, ref StopThrowEvent args)
    {
        if (!ent.Comp.BeingTabled)
            return;

        ent.Comp.BeingTabled = false;
        Dirty(ent);
    }

    /// <summary>
    /// A shove right after the slam can stun.
    /// </summary>
    private void OnDisarmAttempt(Entity<PostTabledComponent> ent, ref DisarmAttemptEvent args)
    {
        var random = new System.Random(SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value, GetNetEntity(ent).Id));
        if (random.NextDouble() >= ent.Comp.ParalyzeChance)
            return;

        _stun.TryParalyze(ent, ent.Comp.ParalyzeTime, true);
        RemCompDeferred<PostTabledComponent>(ent);
    }
}
