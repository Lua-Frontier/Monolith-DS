using Content.Server.NPC.HTN;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Cyberware;

/// <summary>
/// Added while a creature is in cyberpsychosis: an NPC controls the body and attacks everyone around.
/// </summary>
[RegisterComponent]
public sealed partial class CyberpsychosisComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;

    /// <summary>
    /// Mind of the player that lost control, watching through <see cref="Viewer"/>.
    /// </summary>
    [ViewVariables]
    public EntityUid? Mind;

    [ViewVariables]
    public EntityUid? Viewer;

    /// <summary>
    /// Whether the NPC was added by the cyberpsychosis and should be removed after it.
    /// </summary>
    [ViewVariables]
    public bool AddedNpc;

    /// <summary>
    /// Root task of an NPC the creature already had, restored after the cyberpsychosis.
    /// </summary>
    [ViewVariables]
    public HTNCompoundTask? OldRootTask;

    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> OldFactions = new();

    /// <summary>
    /// When the psycho tries to turn its implants on again.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextImplantUse;

    [DataField]
    public TimeSpan ImplantUseInterval = TimeSpan.FromSeconds(1);
}
