// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Content.Shared.Humanoid;

namespace Content.Shared.DeadSpace.Necromorphs.Unitology.Components;

/// <summary>
/// Marks a unitology slave and stores the appearance needed to undo their partial necromorph transformation.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnitologyEnslavedComponent : Component
{
    /// <summary>
    /// The status icon prototype displayed for enslaved by unitologs
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "UnitologyEnslavedFaction";

    /// <summary>
    /// Original appearance data before the partial transformation.
    /// </summary>
    [DataField]
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> OriginalCustomBaseLayers = new();

    /// <summary>
    /// Original skin color before transformation.
    /// </summary>
    [DataField]
    public Color OriginalSkinColor;

    /// <summary>
    /// Original eye color before transformation.
    /// </summary>
    [DataField]
    public Color OriginalEyeColor;

    /// <summary>
    /// Whether the partial transformation has been applied.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsTransformed { get; set; }

    /// <summary>
    /// Head whose death releases this slave.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? HeadUnitolog { get; set; }

    /// <summary>
    /// These flags prevent restoration from removing immunities the species already had.
    /// </summary>
    [DataField]
    public bool AddedBreathingImmunity;

    [DataField]
    public bool AddedPressureImmunity;

    public override bool SessionSpecific => true;
}
