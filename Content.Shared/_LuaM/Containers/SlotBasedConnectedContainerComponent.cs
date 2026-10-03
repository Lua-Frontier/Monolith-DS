// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Containers;

/// <summary>
/// Links an item to a compatible container equipped in a configured inventory slot.
/// </summary>
[RegisterComponent, Access(typeof(SlotBasedConnectedContainerSystem)), NetworkedComponent]
public sealed partial class SlotBasedConnectedContainerComponent : Component
{
    [DataField(required: true)]
    public SlotFlags TargetSlot;

    [DataField]
    public EntityWhitelist? ContainerWhitelist;
}
