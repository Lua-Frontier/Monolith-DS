// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Drones.Components;

[RegisterComponent]
public sealed partial class DetonateAttachedExplosivesComponent : Component
{
    [DataField]
    public EntityWhitelist? ExplosiveWhitelist;

    [DataField]
    public EntProtoId Action = "ToyCarDetonateAttachedExplosives";

    /// <summary>
    /// Maximum number of matching explosives that can be attached at once.
    /// </summary>
    [DataField]
    public int MaxAttachedExplosives = 1;

    public EntityUid? ActionEntity;
}

public sealed partial class DetonateAttachedExplosivesActionEvent : InstantActionEvent;
