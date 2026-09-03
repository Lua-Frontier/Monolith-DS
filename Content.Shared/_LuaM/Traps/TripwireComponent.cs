// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Traps;

[RegisterComponent]
public sealed partial class TripwireComponent : Component
{
    [DataField]
    public ProtoId<SourcePortPrototype> Port = "Pressed";

    [DataField]
    public string ImmediateTriggerKey = "timer";

    [DataField]
    public float DisarmTime = 10f;

    [DataField]
    public float AttachTime = 1f;

    [DataField]
    public float AttachedExplosiveFadeTime = 10f;

    [DataField]
    public float DetachExplosiveTime = 30f;

    [DataField]
    public bool Triggered;

    [ViewVariables]
    public HashSet<EntityUid> LinkedTargets = new();

    [ViewVariables]
    public HashSet<EntityUid> AttachedExplosives = new();
}

[Serializable, NetSerializable]
public sealed partial class TripwireDisarmDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TripwireAttachExplosiveDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TripwireDetachExplosiveDoAfterEvent : SimpleDoAfterEvent;
