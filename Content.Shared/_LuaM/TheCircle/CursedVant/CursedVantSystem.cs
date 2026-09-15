// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._LuaM.TheCircle.CursedVant;

public sealed class CursedVantSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursedVantComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<CursedVantComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<CursedVantComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CursedVantComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CursedVantComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnHeldRefresh);
        SubscribeLocalEvent<CursedVantComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnEquippedRefresh);
    }

    private void OnEquippedHand(Entity<CursedVantComponent> ent, ref GotEquippedHandEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnUnequippedHand(Entity<CursedVantComponent> ent, ref GotUnequippedHandEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnEquipped(Entity<CursedVantComponent> ent, ref GotEquippedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.Equipee);
    }

    private void OnUnequipped(Entity<CursedVantComponent> ent, ref GotUnequippedEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(args.Equipee);
    }

    private void OnHeldRefresh(
        Entity<CursedVantComponent> ent,
        ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var holder = Transform(ent).ParentUid;
        if (HasComp<CircleDeaconComponent>(holder))
            return;

        args.Args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    private void OnEquippedRefresh(
        Entity<CursedVantComponent> ent,
        ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (HasComp<CircleDeaconComponent>(Transform(ent).ParentUid))
            return;

        args.Args.ModifySpeed(ent.Comp.SpeedModifier);
    }
}
