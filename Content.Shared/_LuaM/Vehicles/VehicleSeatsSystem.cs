/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Buckle;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Projectiles;
using Content.Shared.Verbs;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleSeatsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedContentEyeSystem _eye = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleSeatsComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<VehicleSeatsComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<VehicleSeatsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<VehicleSeatsComponent, PreventCollideEvent>(OnVehiclePreventCollide);
        SubscribeLocalEvent<VehicleSeatsComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);

        SubscribeLocalEvent<VehicleOccupantComponent, PreventCollideEvent>(OnOccupantPreventCollide);
        SubscribeLocalEvent<VehicleOccupantComponent, AttackAttemptEvent>(OnOccupantAttackAttempt);
    }

    private void OnStrapped(Entity<VehicleSeatsComponent> ent, ref StrappedEvent args)
    {
        var occupant = args.Buckle.Owner;
        var seat = _buckle.GetSlotIndex(ent, Transform(occupant).LocalPosition);
        Seat(ent, occupant, seat);
    }

    private void OnUnstrapped(Entity<VehicleSeatsComponent> ent, ref UnstrappedEvent args)
    {
        Unseat(ent, args.Buckle.Owner);
    }

    private void Seat(Entity<VehicleSeatsComponent> ent, EntityUid occupant, int seat)
    {
        var role = seat >= 0 && seat < ent.Comp.Seats.Count ? ent.Comp.Seats[seat] : VehicleSeatRole.Passenger;

        var comp = EnsureComp<VehicleOccupantComponent>(occupant);
        comp.Vehicle = ent;
        comp.Role = role;
        comp.Seat = seat;
        Dirty(occupant, comp);
        _physics.RegenerateContacts(occupant);
        SetVehicleZoom(ent, occupant, comp);

        switch (role)
        {
            case VehicleSeatRole.Driver:
                ent.Comp.Driver = occupant;
                BlockHands(ent, occupant, 1);
                AddActions(ent, occupant, ent.Comp.DriverActions);
                break;
            case VehicleSeatRole.Gunner:
                ent.Comp.Gunner = occupant;
                BlockHands(ent, occupant, CompOrNull<HandsComponent>(occupant)?.Count ?? 0);
                AddActions(ent, occupant, ent.Comp.GunnerActions);
                break;
        }

        Dirty(ent);

        var ev = new VehicleSeatsChangedEvent(occupant, role, true);
        RaiseLocalEvent(ent, ref ev);
    }

    private void Unseat(Entity<VehicleSeatsComponent> ent, EntityUid occupant)
    {
        if (!TryComp<VehicleOccupantComponent>(occupant, out var comp) || comp.Vehicle != ent.Owner)
            return;

        var role = comp.Role;
        _actions.RemoveProvidedActions(occupant, ent);
        _virtualItem.DeleteInHandsMatching(occupant, ent);

        if (ent.Comp.Driver == occupant)
            ent.Comp.Driver = null;

        if (ent.Comp.Gunner == occupant)
            ent.Comp.Gunner = null;

        Dirty(ent);
        if (comp.PreviousMaxZoom is { } maxZoom)
            _eye.SetMaxZoom(occupant, maxZoom);

        _eye.ResetZoom(occupant);
        RemComp<VehicleOccupantComponent>(occupant);
        _physics.RegenerateContacts(occupant);

        var ev = new VehicleSeatsChangedEvent(occupant, role, false);
        RaiseLocalEvent(ent, ref ev);
    }

    // -- Предел зума поднимаем до машинного, иначе после ручного приближения отдалиться обратно уже нельзя
    private void SetVehicleZoom(Entity<VehicleSeatsComponent> ent, EntityUid occupant, VehicleOccupantComponent comp)
    {
        if (!TryComp<ContentEyeComponent>(occupant, out var eye))
            return;

        comp.PreviousMaxZoom ??= eye.MaxZoom;
        var max = Vector2.Max(comp.PreviousMaxZoom.Value, ent.Comp.Zoom);
        _eye.SetMaxZoom(occupant, max, eye);
        _eye.SetZoom(occupant, ent.Comp.Zoom, eye: eye);
    }

    // -- Водитель держит руль одной рукой и второй может стрелять, у стрелка заняты обе; выбросить блокировку нельзя
    private void BlockHands(EntityUid vehicle, EntityUid occupant, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!_virtualItem.TrySpawnVirtualItemInHand(vehicle, occupant, out var item, dropOthers: true))
                break;

            EnsureComp<UnremoveableComponent>(item.Value);
        }
    }

    private void AddActions(EntityUid vehicle, EntityUid user, List<Robust.Shared.Prototypes.EntProtoId> actions)
    {
        foreach (var action in actions)
        {
            _actions.AddAction(user, action, vehicle);
        }
    }

    public bool IsSeatFree(Entity<VehicleSeatsComponent> ent, int seat)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return false;

        foreach (var buckled in strap.BuckledEntities)
        {
            if (TryComp<VehicleOccupantComponent>(buckled, out var occupant) && occupant.Seat == seat)
                return false;
        }

        return true;
    }

    public void SwitchSeat(Entity<VehicleSeatsComponent> ent, EntityUid occupant, int seat)
    {
        if (!TryComp<StrapSlotsComponent>(ent, out var slots) ||
            seat < 0 || seat >= slots.Offsets.Count ||
            !IsSeatFree(ent, seat))
        {
            return;
        }

        Unseat(ent, occupant);
        _transform.SetLocalPositionNoLerp(occupant, slots.Offsets[seat]);
        Seat(ent, occupant, seat);
    }

    private void OnGetVerbs(Entity<VehicleSeatsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<StrapSlotsComponent>(ent, out var slots))
            return;

        var user = args.User;
        var seated = TryComp<VehicleOccupantComponent>(user, out var occupant) && occupant.Vehicle == ent.Owner;
        if (!seated && !HasComp<BuckleComponent>(user))
            return;

        var passenger = 0;
        for (var i = 0; i < ent.Comp.Seats.Count && i < slots.Offsets.Count; i++)
        {
            var role = ent.Comp.Seats[i];
            var name = role switch
            {
                VehicleSeatRole.Driver => Loc.GetString("luam-vehicle-seat-driver"),
                VehicleSeatRole.Gunner => Loc.GetString("luam-vehicle-seat-gunner"),
                _ => Loc.GetString("luam-vehicle-seat-passenger", ("number", ++passenger)),
            };

            if (seated && occupant!.Seat == i || !IsSeatFree(ent, i))
                continue;

            var seat = i;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(seated ? "luam-vehicle-verb-switch-seat" : "luam-vehicle-verb-take-seat", ("seat", name)),
                Category = VerbCategory.Buckle,
                Priority = -seat,
                Act = () =>
                {
                    if (seated)
                    {
                        SwitchSeat(ent, user, seat);
                        return;
                    }

                    slots.Requested[user] = seat;
                    _buckle.TryBuckle(user, user, ent);
                    slots.Requested.Remove(user);
                },
            });
        }
    }

    // -- Из машины выходят сами: вытащить другого можно, только если он в крите или мёртв
    private void OnUnstrapAttempt(Entity<VehicleSeatsComponent> ent, ref UnstrapAttemptEvent args)
    {
        var occupant = args.Buckle.Owner;
        if (args.Cancelled || args.User is not { } user || user == occupant || _mobState.IsIncapacitated(occupant))
            return;

        args.Cancelled = true;
        if (args.Popup)
            _popup.PopupClient(Loc.GetString("luam-vehicle-cannot-pull-out"), ent, user);
    }

    private void OnVehiclePreventCollide(Entity<VehicleSeatsComponent> ent, ref PreventCollideEvent args)
    {
        if (IsAboard(ent, args.OtherEntity) || IsOwnShot(ent, args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnOccupantPreventCollide(Entity<VehicleOccupantComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OtherEntity == ent.Comp.Vehicle || IsAboard(ent.Comp.Vehicle, args.OtherEntity) || IsOwnShot(ent.Comp.Vehicle, args.OtherEntity))
            args.Cancelled = true;
    }

    private bool IsAboard(EntityUid vehicle, EntityUid other)
    {
        return TryComp<BuckleComponent>(other, out var buckle) && buckle.BuckledTo == vehicle;
    }

    private bool IsOwnShot(EntityUid vehicle, EntityUid other)
    {
        if (!TryComp<ProjectileComponent>(other, out var projectile))
            return false;

        if (projectile.Shooter == vehicle)
            return true;

        return projectile.Shooter is { } shooter &&
               TryComp<VehicleOccupantComponent>(shooter, out var occupant) &&
               occupant.Vehicle == vehicle;
    }

    private void OnOccupantAttackAttempt(Entity<VehicleOccupantComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target == ent.Comp.Vehicle)
            args.Cancel();
    }

    public bool TryGetDriver(EntityUid vehicle, out EntityUid driver)
    {
        driver = default;
        if (!TryComp<VehicleSeatsComponent>(vehicle, out var seats) || seats.Driver is not { } d)
            return false;

        driver = d;
        return true;
    }
}
