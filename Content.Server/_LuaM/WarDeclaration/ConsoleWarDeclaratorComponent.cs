using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.WarDeclaration;

/// <summary>
/// Lets a communications console declare war for its faction during a manual portstrike round.
/// </summary>
[RegisterComponent]
public sealed partial class ConsoleWarDeclaratorComponent : Component
{
    /// <summary>
    /// Which faction this console declares war for. Only members of this faction can use it.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CompanyPrototype> Faction;

    /// <summary>
    /// The announcement sent when war is declared.
    /// </summary>
    [DataField(required: true)]
    public LocId WarDeclarationMessage;

    /// <summary>
    /// How long the round has to last before war can be declared.
    /// </summary>
    [DataField]
    public TimeSpan MinRoundTime = TimeSpan.FromHours(3);
}
