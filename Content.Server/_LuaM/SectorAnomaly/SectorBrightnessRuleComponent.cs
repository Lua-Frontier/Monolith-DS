namespace Content.Server._LuaM.SectorAnomaly;

[RegisterComponent]
public sealed partial class SectorBrightnessRuleComponent : SectorAnomalyRuleComponent
{
    [DataField]
    public Color AmbientColor = Color.FromHex("#5A6474");

    [DataField]
    public float LightEnergyMultiplier = 1f;

    [DataField]
    public float LightSaturationMultiplier = 1f;

    [ViewVariables]
    public bool AddedMapLight;

    /// <summary>
    /// Original energy and color of boosted lights, plus the boosted energy to detect lights changed since.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, (float Energy, Color Color, float Boosted)> BoostedLights = new();
}
