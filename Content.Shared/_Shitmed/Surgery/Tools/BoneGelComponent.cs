using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Tools;

[RegisterComponent, NetworkedComponent]
public sealed partial class BoneGelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "bone gel"; // LuaM: bone gel > itemswitch-component-state-bonegel

    public bool? Used { get; set; } = null;

    [DataField]
    public float Speed { get; set; } = 1f;
}
