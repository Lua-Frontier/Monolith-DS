using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Xenoarchaeology;

/// <summary>
/// Multiplies the research points of the artifact node it is on.
/// Added to nodes by triggers that should be worth more than usual.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoArtifactNodePointMultiplierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
