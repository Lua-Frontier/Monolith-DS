using Content.Shared.FixedPoint;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Lowers every damage threshold of the mob, like crit and death, so it goes down sooner.
/// </summary>
[RegisterComponent]
public sealed partial class MobThresholdsDecreaseComponent : Component
{
    [DataField(required: true)]
    public FixedPoint2 Amount;
}
