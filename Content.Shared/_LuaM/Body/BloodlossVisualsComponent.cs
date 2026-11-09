using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Body;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodlossVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Intensity;

    [DataField, AutoNetworkedField]
    public Color BloodColor = Color.FromHex("#800000");

    public override bool SendOnlyToOwner => true;
}
