using Robust.Shared.Prototypes;

namespace Content.Server._Lua.EmpGenerator;

/// <summary>
/// A machine that fires a large EMP pulse when its <see cref="Power.Components.PowerChargeComponent"/> is charged
/// and the action button in its UI is pressed. The grid it stands on is not affected.
/// </summary>
[RegisterComponent]
[Access(typeof(EmpGeneratorSystem))]
public sealed partial class EmpGeneratorComponent : Component
{
    /// <summary>
    /// The range of the EMP pulse, in meters.
    /// </summary>
    [DataField]
    public float Range = 100f;

    /// <summary>
    /// How much energy is drained from every battery in range.
    /// </summary>
    [DataField]
    public float EnergyConsumption = 1000000f;

    /// <summary>
    /// How long the targets stay disabled.
    /// </summary>
    [DataField]
    public TimeSpan DisableDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public float LightRadiusMin;

    [DataField]
    public float LightRadiusMax;

    [DataField]
    public EntProtoId EmpBlastEffect = "EffectEmpBlast";

    /// <summary>
    /// Skip <see cref="Shared.Emp.EmpAttemptEvent"/>, so EMP-immune entities are affected too.
    /// </summary>
    [DataField]
    public bool BypassEmpImmunity;
}
