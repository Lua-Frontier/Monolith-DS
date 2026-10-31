using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Sandevistan;

public sealed partial class SandevistanGunBlockSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SandevistanGunBlockComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<SandevistanGunBlockComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();

        if (_timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = _timing.CurTime + ent.Comp.PopupCooldown;
        _popup.PopupClient(Loc.GetString("sandevistan-no-guns"), ent, ent);
    }
}
