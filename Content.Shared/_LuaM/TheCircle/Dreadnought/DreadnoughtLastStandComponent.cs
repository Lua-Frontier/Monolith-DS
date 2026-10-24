// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.TheCircle.Dreadnought;

[RegisterComponent]
public sealed partial class DreadnoughtLastStandComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionDreadnoughtLastStand";

    [DataField]
    public EntityUid? ActionEntity;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(5);

    [DataField]
    public float SpeedModifier = 1.3f;

    [DataField]
    public float DeathDamage = 165f;

    [DataField]
    public TimeSpan BuckleDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan StrapBreakStunDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public SlotFlags RequiredSlots = SlotFlags.OUTERCLOTHING;

    [DataField]
    public bool Used;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DreadnoughtLastStandActiveComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.3f;

    [DataField, AutoNetworkedField]
    public float DeathDamage = 165f;

    [AutoNetworkedField, ViewVariables]
    public bool Expired;

    [ViewVariables]
    public bool AppliedIgnoreSlowOnDamage;
}

public sealed partial class DreadnoughtLastStandActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class DreadnoughtBuckleDoAfterEvent : SimpleDoAfterEvent;
