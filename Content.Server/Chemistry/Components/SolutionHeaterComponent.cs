namespace Content.Server.Chemistry.Components;

[RegisterComponent]
public sealed partial class SolutionHeaterComponent : Component
{
    /// <summary>
    /// How much heat is added per second to the solution, taking upgrades into account.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HeatPerSecond;

    /// <summary>
    /// How much heat is added per second to the solution, with no upgrades.
    /// </summary>
    [DataField("baseHeatPerSecond")]
    public float BaseHeatPerSecond = 120;

    /// <summary>
    /// The machine part that affects the heat multiplier.
    /// </summary>
    [DataField("machinePartHeatMultiplier")]
    public string MachinePartHeatMultiplier = "Capacitor";

    /// <summary>
    /// How much each upgrade multiplies the heat by.
    /// </summary>
    [DataField("partRatingHeatMultiplier")]
    public float PartRatingHeatMultiplier = 1.5f;

    // LuaM-start
    /// <summary>
    /// Whether the heater removes heat from the solution instead of adding it.
    /// </summary>
    [DataField]
    public bool Cooling;

    /// <summary>
    /// Solutions are not cooled below this temperature, in Kelvin.
    /// </summary>
    [DataField]
    public float MinTemperature = 253.15f;
    // LuaM-end
}
