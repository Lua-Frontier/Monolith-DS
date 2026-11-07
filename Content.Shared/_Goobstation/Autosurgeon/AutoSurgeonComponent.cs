using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Tools; // LuaM
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.Autosurgeon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutoSurgeonComponent : Component
{
    /// <summary>
    /// Body part to be upgraded. Specify the parent for replacing body part or organ.
    /// </summary>
    [DataField]
    public BodyPartType TargetBodyPart = BodyPartType.Other;

    /// <summary>
    /// Symmetry of the body part to be upgraded. Specify the parent for replacing body part or organ.
    /// </summary>
    [DataField]
    public BodyPartSymmetry TargetBodyPartSymmetry = BodyPartSymmetry.None;

    /// <summary>
    /// If it's null, will upgrade/replace the body part. If not, will search for this organ to upgrade.
    /// For replacing organs, you can write whatever you want here, it just shouldn't be null.
    /// </summary>
    [DataField]
    public string? TargetOrgan;

    /// <summary>
    /// These components will be added to organ as BodyPart/Organ.OnAdd and applied to the user, for example, LeftMantisBladeUser.
    /// This is only applied if NewPartProto is null, so we are upgrading and not replacing a part.
    /// </summary>
    [DataField]
    public ComponentRegistry? ComponentsToUser;

    /// <summary>
    /// These components will be added to the part itself, for example, MantisBladeArm.
    /// If this is not null but the targeted part has all of them already, the surgery would fail.
    /// This is only applied if NewPartProto is null, so we are upgrading and not replacing a part.
    /// </summary>
    [DataField]
    public ComponentRegistry? ComponentsToPart;

    /// <summary>
    /// If it's null, will upgrade the body part/organ. If not, will replace it with this proto.
    /// </summary>
    [DataField]
    public string? NewPartProto;

    // LuaM-start
    /// <summary>
    /// If not null, installs this subdermal implant into the user instead of touching body parts or organs.
    /// </summary>
    [DataField]
    public EntProtoId? Implant;

    /// <summary>
    /// If not null, puts a new organ of this prototype into every body part of <see cref="TargetBodyPart"/> type,
    /// like a mantis blade into both arms. Parts that already have their slot taken are skipped.
    /// </summary>
    [DataField]
    public EntProtoId? OrganForEachPart;

    /// <summary>
    /// Humanity cost of an upgrade that only adds components, like dermal armor.
    /// Installed organs, parts and implants have their own cost instead.
    /// </summary>
    [DataField]
    public float HumanityCost;

    /// <summary>
    /// Upgrades with the same key share one humanity cost, so a better version replaces the cost instead of adding to it.
    /// The autosurgeon prototype is used if null.
    /// </summary>
    [DataField]
    public string? HumanityKey;

    /// <summary>
    /// If the user already has one of <see cref="ComponentsToUser"/>, overwrite it with this autosurgeon's values
    /// instead of skipping it. Used by improved versions, like platelet factories Mk II.
    /// </summary>
    [DataField]
    public bool ReplaceUserComponents;

    /// <summary>
    /// Tier of this upgrade. A replacing upgrade only works if its tier is higher than the one already installed.
    /// 0 means no tier.
    /// </summary>
    [DataField]
    public int UpgradeTier;
    // LuaM-end

    [DataField]
    public bool OneTimeUse = true;

    [DataField, AutoNetworkedField]
    public bool Used;

    [DataField]
    public TimeSpan DoAfterTime = TimeSpan.FromSeconds(15);

    [DataField] // If you're changing this, do not forget the loop
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/_Goobstation/Machines/autosurgeon.ogg", AudioParams.Default.WithLoop(true));

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActiveSound;

    // LuaM-start
    [DataField]
    public ProtoId<ToolQualityPrototype> DisassembleQuality = "Screwing";

    [DataField]
    public float DisassembleTime = 5f;

    [DataField]
    public Dictionary<EntProtoId, int> DisassembleMaterials = new()
    {
        ["SheetSteel1"] = 5,
        ["SheetPlastic1"] = 5,
    };
    // LuaM-end
}

[Serializable, NetSerializable]
public sealed partial class AutoSurgeonDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AutoSurgeonDisassembleDoAfterEvent : SimpleDoAfterEvent; // LuaM
