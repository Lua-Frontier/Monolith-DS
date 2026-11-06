using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Given by a trait. Puts the organ into every body part of the given type, like a mantis blade into each arm,
/// then removes itself.
/// </summary>
[RegisterComponent]
public sealed partial class TraitOrganForEachPartComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Organ;

    [DataField]
    public BodyPartType PartType = BodyPartType.Arm;
}
