using Content.Shared._LuaM.Addiction;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Addiction;

/// <summary>
/// How much of each addictive chemical a body took in lately. Added the first time it takes one in.
/// </summary>
[RegisterComponent, Access(typeof(AddictionSystem))]
public sealed partial class AddictionTrackerComponent : Component
{
    [ViewVariables]
    public Dictionary<ProtoId<AddictionPrototype>, AddictionIntake> Intake = new();
}

public sealed class AddictionIntake
{
    /// <summary>
    /// Intake grouped into short stretches of time, oldest first.
    /// </summary>
    [ViewVariables]
    public List<(TimeSpan Start, FixedPoint2 Amount)> Buckets = new();

    /// <summary>
    /// The intake within the addiction's window.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 Total;

    /// <summary>
    /// Whether the body was told it is close to the addiction.
    /// </summary>
    [ViewVariables]
    public bool Warned;
}
