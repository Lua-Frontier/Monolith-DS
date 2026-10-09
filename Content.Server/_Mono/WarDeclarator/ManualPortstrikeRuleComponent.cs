using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.WarDeclarator;

[RegisterComponent]
public sealed partial class ManualPortstrikeRuleComponent : Component
{
    /// <summary>
    /// Which factions are involved in portstrike and whether or not they have declared hostilities.
    /// Must be defined in the YAML definition of the gamerule.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<CompanyPrototype>, bool> SectorStatus;

    /// <summary>
    /// War level to set it to once all sides declare war. True for HOT, false for COLD.
    /// </summary>
    [DataField]
    public bool WarLevel = true;

    // LuaM start: automatic declaration for the side that did not answer
    /// <summary>
    /// If one side declared war and the others did not within this time, war is declared for them automatically.
    /// </summary>
    [DataField]
    public TimeSpan AutoDeclareDelay = TimeSpan.FromMinutes(30);

    /// <summary>
    /// When the first side declared war.
    /// </summary>
    [ViewVariables]
    public TimeSpan? FirstDeclarationTime;
    // LuaM end
}