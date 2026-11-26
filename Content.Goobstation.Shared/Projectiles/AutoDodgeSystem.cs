// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.Projectiles;

/// <summary>
/// Entities with <see cref="AutoDodgeComponent"/> dodge projectiles and play a dodge animation.
/// </summary>
public sealed class AutoDodgeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoDodgeComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<AutoDodgeComponent, ProjectileReflectAttemptEvent>(OnProjectileReflect);
        SubscribeLocalEvent<AutoDodgeComponent, HitScanReflectAttemptEvent>(OnHitscanReflect);
        SubscribeLocalEvent<AutoDodgeComponent, GettingAttackedAttemptEvent>(OnGettingAttacked);
    }

    /// <summary>
    /// Only someone standing on their feet and able to move can dodge.
    /// </summary>
    private bool CanDodge(EntityUid uid)
    {
        return _mobState.IsAlive(uid)
               && !HasComp<KnockedDownComponent>(uid)
               && !HasComp<StunnedComponent>(uid)
               && !_standing.IsDown(uid);
    }

    private void OnPreventCollide(Entity<AutoDodgeComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled
            || !ent.Comp.DodgeRanged
            || !args.OtherFixture.Hard
            || !HasComp<ProjectileComponent>(args.OtherEntity)
            || !CanDodge(ent))
            return;

        args.Cancelled = true;
        HandleProjectileDodge(ent, args.OtherEntity);
    }

    private void OnProjectileReflect(Entity<AutoDodgeComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.DodgeRanged || !CanDodge(ent))
            return;

        args.Cancelled = true;
        HandleProjectileDodge(ent, args.ProjUid);
    }

    private void OnHitscanReflect(Entity<AutoDodgeComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected || !ent.Comp.DodgeRanged || !CanDodge(ent))
            return;

        // The shot keeps going in the same direction
        args.Reflected = true;
        DoDodgeEffects(ent, args.Direction, _random.Prob(0.5f));
    }

    private void OnGettingAttacked(Entity<AutoDodgeComponent> ent, ref GettingAttackedAttemptEvent args)
    {
        if (args.Cancelled || !ent.Comp.DodgeMelee || args.Attacker == ent.Owner || !CanDodge(ent))
            return;

        args.Cancelled = true;

        var direction = _xform.GetWorldPosition(ent) - _xform.GetWorldPosition(args.Attacker);
        DoDodgeEffects(ent, direction, _random.Prob(0.5f));
    }

    private void HandleProjectileDodge(Entity<AutoDodgeComponent> ent, EntityUid projectile)
    {
        if (TerminatingOrDeleted(projectile))
            return;

        // Derived from the projectile id instead of RNG so client prediction picks the same side.
        var flipSide = (GetNetEntity(projectile).Id & 1) == 0;
        DoDodgeEffects(ent, _physics.GetMapLinearVelocity(projectile), flipSide);
    }

    private void DoDodgeEffects(Entity<AutoDodgeComponent> ent, Vector2 shotDirection, bool flipSide)
    {
        if (_net.IsClient || _timing.CurTime < ent.Comp.NextEffectTime)
            return;

        ent.Comp.NextEffectTime = _timing.CurTime + ent.Comp.EffectCooldown;
        Dirty(ent);

        var direction = ComputeDodgeDirection(shotDirection, flipSide);
        var ev = new AutoDodgeEffectEvent(GetNetEntity(ent), new Vector2(MathF.Sign(direction.X), 0f));
        RaiseNetworkEvent(ev, Filter.Pvs(ent, entityManager: EntityManager));

        _audio.PlayPvs(ent.Comp.DodgeSound, ent);
    }

    /// <summary>
    /// Picks the direction the sprite dodges towards.
    /// </summary>
    private static Vector2 ComputeDodgeDirection(Vector2 shotDirection, bool flipSide)
    {
        var sidestep = shotDirection.LengthSquared() > 0f
            ? Vector2.Normalize(new Vector2(-shotDirection.Y, shotDirection.X))
            : Vector2.UnitX;

        if (flipSide)
            sidestep = -sidestep;

        var signX = sidestep.X != 0f ? MathF.Sign(sidestep.X) : MathF.Sign(sidestep.Y);
        var x = signX * MathF.Max(MathF.Abs(sidestep.X), 0.8f);
        var y = Math.Clamp(sidestep.Y, -0.35f, 0.35f);
        return Vector2.Normalize(new Vector2(x, y));
    }
}
