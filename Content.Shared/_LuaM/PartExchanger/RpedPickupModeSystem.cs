using Content.Shared.Construction.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._LuaM.PartExchanger;

public sealed partial class RpedPickupModeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    /// <summary>
    /// Set while the part exchanger puts replaced parts back into the RPED, so they are never rejected and dropped.
    /// </summary>
    public bool IgnoreFilters;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RpedPickupModeComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<RpedPickupModeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RpedPickupModeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<RpedPickupModeComponent, GetVerbsEvent<Verb>>(OnGetEmptyVerb);
        SubscribeLocalEvent<RpedPickupModeComponent, RpedEmptyDoAfterEvent>(OnEmptyDoAfter);
    }

    private void OnInsertAttempt(Entity<RpedPickupModeComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (IgnoreFilters || args.Cancelled || args.Container.ID != StorageComponent.ContainerId)
            return;

        // With pickup off only items inserted from hands (still in the hand container at this point) are accepted.
        if (!ent.Comp.PickupEnabled && !_container.IsEntityInContainer(args.EntityUid))
        {
            args.Cancel();
            return;
        }

        if (!IsAllowed(ent.Comp.Mode, args.EntityUid))
            args.Cancel();
    }

    public bool IsAllowed(RpedPickupMode mode, EntityUid item)
    {
        // Power cells are machine parts too (part type PowerCell), so they have to be excluded from components explicitly.
        var isCell = HasComp<PowerCellComponent>(item);
        var isPart = HasComp<MachinePartComponent>(item);

        return mode switch
        {
            RpedPickupMode.Components => isPart && !isCell,
            RpedPickupMode.Batteries => isCell,
            _ => isPart || isCell,
        };
    }

    private void OnExamined(Entity<RpedPickupModeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("rped-pickup-mode-examine", ("mode", ModeName(ent.Comp.Mode))));
        args.PushMarkup(Loc.GetString(ent.Comp.PickupEnabled ? "rped-pickup-examine-on" : "rped-pickup-examine-off"));
    }

    private void OnGetVerbs(Entity<RpedPickupModeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => CycleMode(ent, user),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Text = Loc.GetString("rped-pickup-mode-verb"),
            Priority = 2,
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TogglePickup(ent, user),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Text = Loc.GetString(ent.Comp.PickupEnabled ? "rped-pickup-toggle-off-verb" : "rped-pickup-toggle-on-verb"),
            Priority = 1,
        });
    }

    private void TogglePickup(Entity<RpedPickupModeComponent> ent, EntityUid user)
    {
        ent.Comp.PickupEnabled = !ent.Comp.PickupEnabled;
        Dirty(ent);

        _popup.PopupClient(Loc.GetString(ent.Comp.PickupEnabled ? "rped-pickup-toggled-on" : "rped-pickup-toggled-off"), ent, user);
    }

    private void OnGetEmptyVerb(Entity<RpedPickupModeComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<StorageComponent>(ent, out var storage) || storage.Container.ContainedEntities.Count == 0)
            return;

        var user = args.User;
        var count = storage.Container.ContainedEntities.Count;
        args.Verbs.Add(new Verb
        {
            Act = () => StartEmpty(ent, user, count),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/drop.svg.192dpi.png")),
            Text = Loc.GetString("rped-empty-verb"),
            Priority = 1,
        });
    }

    private void StartEmpty(Entity<RpedPickupModeComponent> ent, EntityUid user, int itemCount)
    {
        var delay = ent.Comp.EmptyDelayMin + ent.Comp.EmptyDelayPerItem * itemCount;
        if (delay > ent.Comp.EmptyDelayMax)
            delay = ent.Comp.EmptyDelayMax;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new RpedEmptyDoAfterEvent(), ent, target: ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnEmptyDoAfter(Entity<RpedPickupModeComponent> ent, ref RpedEmptyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!TryComp<StorageComponent>(ent, out var storage) || storage.Container.ContainedEntities.Count == 0)
            return;

        _container.EmptyContainer(storage.Container, destination: Transform(args.User).Coordinates);
        _popup.PopupClient(Loc.GetString("rped-empty-done"), ent, args.User);
    }

    private void CycleMode(Entity<RpedPickupModeComponent> ent, EntityUid user)
    {
        ent.Comp.Mode = ent.Comp.Mode switch
        {
            RpedPickupMode.Components => RpedPickupMode.Batteries,
            RpedPickupMode.Batteries => RpedPickupMode.Mixed,
            _ => RpedPickupMode.Components,
        };
        Dirty(ent);

        _popup.PopupClient(Loc.GetString("rped-pickup-mode-switched", ("mode", ModeName(ent.Comp.Mode))), ent, user);
    }

    private string ModeName(RpedPickupMode mode)
    {
        return Loc.GetString(mode switch
        {
            RpedPickupMode.Components => "rped-pickup-mode-components",
            RpedPickupMode.Batteries => "rped-pickup-mode-batteries",
            _ => "rped-pickup-mode-mixed",
        });
    }
}
