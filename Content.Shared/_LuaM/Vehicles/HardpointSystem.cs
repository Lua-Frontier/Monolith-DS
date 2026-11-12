/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class HardpointSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private const string PryingQuality = "Prying";
    private const float RemoveDelay = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HardpointSlotsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HardpointSlotsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HardpointSlotsComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HardpointSlotsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HardpointSlotsComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HardpointSlotsComponent, HardpointInstallDoAfterEvent>(OnInstallDoAfter);
        SubscribeLocalEvent<HardpointSlotsComponent, HardpointRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<HardpointSlotsComponent, VehicleAmmoLoadDoAfterEvent>(OnAmmoDoAfter);
        SubscribeLocalEvent<HardpointSlotsComponent, VehicleCanMoveEvent>(OnCanMove);
    }

    private void OnInit(Entity<HardpointSlotsComponent> ent, ref ComponentInit args)
    {
        foreach (var slot in ent.Comp.Slots)
        {
            _container.EnsureContainer<ContainerSlot>(ent, slot.Id);
        }
    }

    private void OnMapInit(Entity<HardpointSlotsComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var slot in ent.Comp.Slots)
        {
            if (slot.StartingItem is not { } proto)
                continue;

            var container = _container.EnsureContainer<ContainerSlot>(ent, slot.Id);
            if (container.ContainedEntity != null)
                continue;

            var item = Spawn(proto, coords);
            if (!_container.Insert(item, container))
                QueueDel(item);
        }
    }

    public IEnumerable<(EntityUid Holder, HardpointSlot Slot, EntityUid? Item)> EnumerateSlots(EntityUid holder)
    {
        if (!TryComp<HardpointSlotsComponent>(holder, out var comp))
            yield break;

        foreach (var slot in comp.Slots)
        {
            var item = _container.TryGetContainer(holder, slot.Id, out var container) && container is ContainerSlot cs
                ? cs.ContainedEntity
                : null;

            yield return (holder, slot, item);

            if (item is { } inner && HasComp<HardpointSlotsComponent>(inner))
            {
                foreach (var nested in EnumerateSlots(inner))
                {
                    yield return nested;
                }
            }
        }
    }

    public IEnumerable<EntityUid> EnumerateInstalled(EntityUid vehicle)
    {
        foreach (var (_, _, item) in EnumerateSlots(vehicle))
        {
            if (item != null)
                yield return item.Value;
        }
    }

    public EntityUid? GetSlotItem(EntityUid holder, string slot)
    {
        return _container.TryGetContainer(holder, slot, out var container) && container is ContainerSlot cs
            ? cs.ContainedEntity
            : null;
    }

    public bool TryGetWeapon(EntityUid vehicle, HardpointType type, [NotNullWhen(true)] out EntityUid? weapon, [NotNullWhen(true)] out GunComponent? gun)
    {
        foreach (var item in EnumerateInstalled(vehicle))
        {
            if (!TryComp<HardpointItemComponent>(item, out var hardpoint) || hardpoint.Type != type ||
                !TryComp(item, out gun))
            {
                continue;
            }

            weapon = item;
            return true;
        }

        weapon = null;
        gun = null;
        return false;
    }

    public bool HasRequiredWheels(EntityUid vehicle)
    {
        foreach (var (_, slot, item) in EnumerateSlots(vehicle))
        {
            if (slot.Type == HardpointType.Wheel && item == null)
                return false;
        }

        return true;
    }

    public float GetStructureDamageMultiplier(EntityUid vehicle)
    {
        var multiplier = 1f;
        foreach (var item in EnumerateInstalled(vehicle))
        {
            if (TryComp<HardpointItemComponent>(item, out var hardpoint))
                multiplier *= hardpoint.StructureDamageMultiplier;
        }

        return multiplier;
    }

    public float GetSpotlightMultiplier(EntityUid vehicle)
    {
        var multiplier = 1f;
        foreach (var item in EnumerateInstalled(vehicle))
        {
            if (TryComp<HardpointItemComponent>(item, out var hardpoint))
                multiplier *= hardpoint.SpotlightRadiusMultiplier;
        }

        return multiplier;
    }

    private void OnCanMove(Entity<HardpointSlotsComponent> ent, ref VehicleCanMoveEvent args)
    {
        if (!HasRequiredWheels(ent))
            args.Cancelled = true;
    }

    private bool TryFindFreeSlot(EntityUid vehicle, HardpointItemComponent item, out EntityUid holder, out HardpointSlot slot)
    {
        foreach (var (h, s, contained) in EnumerateSlots(vehicle))
        {
            if (contained != null || s.Type != item.Type ||
                !TryComp<HardpointSlotsComponent>(h, out var holderComp) || holderComp.Family != item.Family)
            {
                continue;
            }

            holder = h;
            slot = s;
            return true;
        }

        holder = default;
        slot = default!;
        return false;
    }

    private void OnInteractUsing(Entity<HardpointSlotsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<VehicleAmmoBoxComponent>(args.Used, out var box))
        {
            args.Handled = true;
            if (!TryFindAmmoTarget(ent, box, out _))
            {
                _popup.PopupClient(Loc.GetString("luam-vehicle-ammo-no-weapon"), ent, args.User);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, box.LoadDelay, new VehicleAmmoLoadDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        if (!TryComp<HardpointItemComponent>(args.Used, out var item))
            return;

        args.Handled = true;
        if (!TryFindFreeSlot(ent, item, out var holder, out var slot))
        {
            _popup.PopupClient(Loc.GetString("luam-hardpoint-no-slot", ("item", args.Used)), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, slot.InsertDelay,
            new HardpointInstallDoAfterEvent(GetNetEntity(holder), slot.Id), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnInstallDoAfter(Entity<HardpointSlotsComponent> ent, ref HardpointInstallDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        args.Handled = true;
        var holder = GetEntity(args.Holder);
        if (!_container.TryGetContainer(holder, args.Slot, out var container) || container.ContainedEntities.Count > 0)
            return;

        if (_container.Insert(used, container))
            _popup.PopupClient(Loc.GetString("luam-hardpoint-installed", ("item", used)), ent, args.User);
    }

    private void OnGetVerbs(Entity<HardpointSlotsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } tool || !_tool.HasQuality(tool, PryingQuality))
            return;

        if (TryComp<VehicleOccupantComponent>(args.User, out var occupant) && occupant.Vehicle == ent.Owner)
            return;

        var user = args.User;
        foreach (var (holder, slot, item) in EnumerateSlots(ent))
        {
            if (item is not { } installed)
                continue;

            var holderNet = GetNetEntity(holder);
            var slotId = slot.Id;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("luam-hardpoint-verb-remove", ("item", installed)),
                Priority = -10,
                Act = () =>
                {
                    _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, RemoveDelay,
                        new HardpointRemoveDoAfterEvent(holderNet, slotId), ent, ent, tool)
                    {
                        BreakOnMove = true,
                        NeedHand = true,
                    });
                },
            });
        }
    }

    private void OnRemoveDoAfter(Entity<HardpointSlotsComponent> ent, ref HardpointRemoveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var holder = GetEntity(args.Holder);
        if (GetSlotItem(holder, args.Slot) is not { } item ||
            !_container.TryGetContainer(holder, args.Slot, out var container))
        {
            return;
        }

        if (!_container.Remove(item, container))
            return;

        _hands.PickupOrDrop(args.User, item);
        _popup.PopupClient(Loc.GetString("luam-hardpoint-removed", ("item", item)), ent, args.User);
    }

    private bool TryFindAmmoTarget(EntityUid vehicle, VehicleAmmoBoxComponent box, out Entity<BallisticAmmoProviderComponent> target)
    {
        foreach (var item in EnumerateInstalled(vehicle))
        {
            if (!TryComp<HardpointItemComponent>(item, out var hardpoint) || hardpoint.AmmoType != box.AmmoType ||
                !TryComp<BallisticAmmoProviderComponent>(item, out var ballistic) ||
                ballistic.Count >= ballistic.Capacity)
            {
                continue;
            }

            target = (item, ballistic);
            return true;
        }

        target = default;
        return false;
    }

    private void OnAmmoDoAfter(Entity<HardpointSlotsComponent> ent, ref VehicleAmmoLoadDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used ||
            !TryComp<VehicleAmmoBoxComponent>(used, out var box) ||
            !TryFindAmmoTarget(ent, box, out var target))
        {
            return;
        }

        args.Handled = true;
        var space = target.Comp.Capacity - target.Comp.Count;
        var loaded = Math.Min(space, box.Amount);
        if (loaded <= 0)
            return;

        _gun.SetBallisticUnspawned(target, target.Comp.UnspawnedCount + loaded);
        box.Amount -= loaded;
        Dirty(used, box);

        _popup.PopupClient(Loc.GetString("luam-vehicle-ammo-loaded", ("amount", loaded), ("weapon", target.Owner)), ent, args.User);

        if (box.Amount <= 0 && _net.IsServer)
            QueueDel(used);
    }

    private void OnExamined(Entity<HardpointSlotsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || HasComp<HardpointItemComponent>(ent))
            return;

        using (args.PushGroup(nameof(HardpointSystem)))
        {
            foreach (var (_, slot, item) in EnumerateSlots(ent))
            {
                var slotName = Loc.GetString($"luam-hardpoint-type-{slot.Type.ToString().ToLowerInvariant()}");
                if (item is not { } installed)
                {
                    args.PushMarkup(Loc.GetString("luam-hardpoint-examine-empty", ("slot", slotName)));
                    continue;
                }

                var ammo = TryComp<BallisticAmmoProviderComponent>(installed, out var ballistic)
                    ? Loc.GetString("luam-hardpoint-examine-ammo", ("count", ballistic.Count), ("capacity", ballistic.Capacity))
                    : string.Empty;

                args.PushMarkup(Loc.GetString("luam-hardpoint-examine-installed", ("slot", slotName), ("item", installed), ("ammo", ammo)));
            }
        }
    }
}
