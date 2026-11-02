using Content.Shared._LuaM.RpPanel.Requirements;
using Content.Shared.Dataset;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._LuaM.RpPanel;

public enum RpInteractionKind : byte
{
    Soft,
    Harmful,
}

[Prototype]
public sealed partial class RpInteractionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<RpInteractionCategoryPrototype> Category;

    [DataField(required: true)]
    public RpInteractionKind Kind;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Messages;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? SelfMessages;

    [DataField(required: true)]
    public SpriteSpecifier Icon = SpriteSpecifier.Invalid;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Range = 1f;

    [DataField]
    public List<RpRequirement>? UserRequirements;

    [DataField]
    public List<RpRequirement>? TargetRequirements;
}

[Prototype]
public sealed partial class RpInteractionCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public SpriteSpecifier Icon = SpriteSpecifier.Invalid;

    [DataField]
    public int Order;
}

[Prototype]
public sealed partial class RpCustomSoundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
