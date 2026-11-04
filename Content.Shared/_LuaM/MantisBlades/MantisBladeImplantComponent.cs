using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.MantisBlades;

/// <summary>
/// Mantis blade organ that sits in an arm implant slot.
/// All blades of a body are extended and retracted together with one action.
/// </summary>
[RegisterComponent]
public sealed partial class MantisBladeImplantComponent : Component
{
    [DataField]
    public EntProtoId BladeProto = "MantisBlade";

    /// <summary>
    /// Action given to the body while it has at least one mantis blade implant.
    /// </summary>
    [DataField]
    public EntProtoId ActionProto = "ActionToggleMantisBlades";

    [ViewVariables]
    public EntityUid? BladeUid;

    [DataField]
    public SoundSpecifier? ExtendSound = new SoundPathSpecifier("/Audio/_Goobstation/Weapons/MantisBlades/mantis_extend.ogg");

    [DataField]
    public SoundSpecifier? RetractSound = new SoundCollectionSpecifier("MantisBladeRetract");
}

/// <summary>
/// Added to a body with mantis blade implants. Keeps the action that toggles all of them.
/// </summary>
[RegisterComponent]
public sealed partial class MantisBladeImplantUserComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionUid;
}

public sealed partial class ToggleMantisBladesEvent : InstantActionEvent;
