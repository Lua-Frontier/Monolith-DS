namespace Content.Server._LuaM.SectorAnomaly;

[RegisterComponent]
public sealed partial class SectorDarknessRuleComponent : SectorAnomalyRuleComponent
{
    [DataField]
    public float LightBreakChancePerSecond;

    [DataField]
    public float DoorToggleChancePerSecond;

    [DataField]
    public float LightFlickerChancePerSecond;
}
