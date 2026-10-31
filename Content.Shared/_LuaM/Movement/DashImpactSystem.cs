using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Movement;

/// <summary>
/// Stops jumps and dashes on the first creature in the way and knocks that creature down.
/// </summary>
public sealed partial class DashImpactSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DashImpactComponent, ThrowDoHitEvent>(OnHit);
        SubscribeLocalEvent<DashImpactComponent, StopThrowEvent>(OnStopThrow);
    }

    /// <summary>
    /// Call right after the performer was thrown by a jump or dash.
    /// </summary>
    public void StartImpact(EntityUid uid)
    {
        if (HasComp<ThrownItemComponent>(uid))
            EnsureComp<DashImpactComponent>(uid);
    }

    private void OnHit(Entity<DashImpactComponent> ent, ref ThrowDoHitEvent args)
    {
        if (args.Target == ent.Owner || !HasComp<MobStateComponent>(args.Target))
            return;

        _stun.TryKnockdown(args.Target, ent.Comp.KnockdownTime, true);

        // Stop the jump here; the throw itself is finished by ThrownItemSystem on the next update,
        // since fixtures can't be changed while the collision is being handled.
        _physics.SetLinearVelocity(ent, Vector2.Zero);
        if (TryComp<ThrownItemComponent>(ent, out var thrown))
        {
            thrown.LandTime = _timing.CurTime;
            Dirty(ent, thrown);
        }

        RemCompDeferred<DashImpactComponent>(ent);
    }

    private void OnStopThrow(Entity<DashImpactComponent> ent, ref StopThrowEvent args)
    {
        RemCompDeferred<DashImpactComponent>(ent);
    }
}
