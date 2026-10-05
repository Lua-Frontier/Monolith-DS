// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Inventory;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared._LuaM.Containers;

public sealed class SlotBasedConnectedContainerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SlotBasedConnectedContainerComponent, GetConnectedContainerEvent>(OnGetContainer);
    }

    public bool TryGetConnectedContainer(EntityUid uid, [NotNullWhen(true)] out EntityUid? slotEntity)
    {
        if (!TryComp<SlotBasedConnectedContainerComponent>(uid, out var component))
        {
            slotEntity = null;
            return false;
        }

        return TryGetConnectedContainer(uid, component.TargetSlot, component.ContainerWhitelist, out slotEntity);
    }

    private void OnGetContainer(
        Entity<SlotBasedConnectedContainerComponent> ent,
        ref GetConnectedContainerEvent args)
    {
        if (TryGetConnectedContainer(ent, ent.Comp.TargetSlot, ent.Comp.ContainerWhitelist, out var container))
            args.ContainerEntity = container;
    }

    private bool TryGetConnectedContainer(
        EntityUid uid,
        SlotFlags slots,
        EntityWhitelist? whitelist,
        [NotNullWhen(true)] out EntityUid? slotEntity)
    {
        slotEntity = null;
        if (!_containers.TryGetContainingContainer((uid, null, null), out var container) ||
            !_inventory.TryGetContainerSlotEnumerator(container.Owner, out var enumerator, slots))
            return false;

        while (enumerator.NextItem(out var item))
        {
            if (_whitelist.IsWhitelistFailOrNull(whitelist, item))
                continue;

            slotEntity = item;
            return true;
        }

        return false;
    }
}

[ByRefEvent]
public struct GetConnectedContainerEvent
{
    public EntityUid? ContainerEntity;
}
