using Content.Shared.Power;

namespace Content.Client._Lua.EmpGenerator;

[RegisterComponent]
[Access(typeof(EmpGeneratorVisualsSystem))]
public sealed partial class EmpGeneratorVisualsComponent : Component
{
    /// <summary>
    /// Base layer state for each machine status.
    /// </summary>
    [DataField]
    public Dictionary<PowerChargeStatus, string> SpriteMap = new();

    /// <summary>
    /// Core layer states by charge, in increasing order of <see cref="EmpCoreSpriteThreshold.MaxCharge"/>.
    /// </summary>
    [DataField]
    public List<EmpCoreSpriteThreshold> Thresholds = new();
}

[DataDefinition]
public partial record struct EmpCoreSpriteThreshold
{
    [DataField]
    public float MaxCharge;

    [DataField]
    public string? State;

    [DataField]
    public bool Visible;
}

public enum EmpGeneratorVisualLayers : byte
{
    Base,
    Core,
}
