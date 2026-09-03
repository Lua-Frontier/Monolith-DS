// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._LuaM.Drones.Prototypes;

/// <summary>
/// Status icon displayed on an entity while its mind is controlling a remote vehicle.
/// </summary>
[Prototype]
public sealed partial class FpvIconPrototype : StatusIconPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<FpvIconPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }
}
