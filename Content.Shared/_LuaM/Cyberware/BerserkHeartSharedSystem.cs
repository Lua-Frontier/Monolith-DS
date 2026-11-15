using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Parts of the berserk heart the client has to predict: no guns and no slowdown from damage.
/// </summary>
public sealed partial class BerserkHeartSharedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan GunPopupCooldown = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BerserkHeartComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<BerserkHeartComponent, ModifySlowOnDamageSpeedEvent>(OnModifySlowOnDamage);
        SubscribeLocalEvent<BerserkHeartComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnShotAttempted(Entity<BerserkHeartComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!ent.Comp.Active)
            return;

        args.Cancel();

        if (_timing.CurTime < ent.Comp.NextGunPopup)
            return;

        ent.Comp.NextGunPopup = _timing.CurTime + GunPopupCooldown;
        _popup.PopupClient(Loc.GetString("berserk-heart-no-guns"), ent, ent);
    }

    private void OnModifySlowOnDamage(Entity<BerserkHeartComponent> ent, ref ModifySlowOnDamageSpeedEvent args)
    {
        if (ent.Comp.Active)
            args.Speed = 1f;
    }

    private void OnState(Entity<BerserkHeartComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }
}
