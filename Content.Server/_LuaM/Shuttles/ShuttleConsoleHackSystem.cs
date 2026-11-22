using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Systems;
using Content.Shared._LuaM.Shuttles;
using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._LuaM.Shuttles;

/// <summary>
/// A cryptographic sequencer breaks the lock of a shuttle console after a long hack,
/// while the console warns everyone on the short-range channel.
/// </summary>
public sealed partial class ShuttleConsoleHackSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ShuttleConsoleLockSystem _lock = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;

    private static readonly TimeSpan HackTime = TimeSpan.FromSeconds(30);
    private const string AnnounceChannel = "Traffic";

    public override void Initialize()
    {
        base.Initialize();

        // Before the sequencer's own instant effect, which runs on the after-interact.
        SubscribeLocalEvent<ShuttleConsoleLockComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ShuttleConsoleLockComponent, ShuttleConsoleHackDoAfterEvent>(OnHackDoAfter);
    }

    private void OnInteractUsing(Entity<ShuttleConsoleLockComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp<EmagComponent>(args.Used, out var emag)
            || emag.Demag
            || !_lock.GetEffectiveLockState(ent, ent.Comp))
            return;

        args.Handled = true;

        if (_charges.IsEmpty(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("emag-no-charges"), args.User, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, HackTime, new ShuttleConsoleHackDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _radio.SendRadioMessage(ent, Loc.GetString("shuttle-console-hack-announcement", ("ship", ShipName(ent))), AnnounceChannel, ent);
        _adminLog.Add(LogType.Emag, LogImpact.High, $"{ToPrettyString(args.User):player} started hacking {ToPrettyString(ent):console}");
    }

    private void OnHackDoAfter(Entity<ShuttleConsoleLockComponent> ent, ref ShuttleConsoleHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        args.Handled = true;

        if (!_lock.GetEffectiveLockState(ent, ent.Comp))
            return;

        if (TryComp<LimitedChargesComponent>(used, out var charges) && !_charges.TryUseCharge((used, charges)))
        {
            _popup.PopupEntity(Loc.GetString("emag-no-charges"), args.User, args.User);
            return;
        }

        _lock.ForceUnlock(ent, ent.Comp);

        if (TryComp<EmagComponent>(used, out var emag))
            _audio.PlayPvs(emag.EmagSound, ent);

        _adminLog.Add(LogType.Emag, LogImpact.High, $"{ToPrettyString(args.User):player} hacked {ToPrettyString(ent):console} open");
    }

    private string ShipName(EntityUid console)
    {
        return Transform(console).GridUid is { } grid ? Name(grid) : Name(console);
    }
}
