namespace Content.Client.PowerCell;

[RegisterComponent]
public sealed partial class PowerCellVisualsComponent : Component
{
    [DataField] // LuaM
    public bool ChargeBars;
}
