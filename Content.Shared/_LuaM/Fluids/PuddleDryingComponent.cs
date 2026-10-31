using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._LuaM.Fluids;

/// <summary>
/// Makes the reagents of a puddle that don't evaporate on their own (everything but water and the like)
/// dry out once <see cref="Delay"/> has passed since the puddle appeared. Water keeps evaporating as before.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PuddleDryingComponent : Component
{
    /// <summary>
    /// How long after the puddle appeared it starts to dry out.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How many units of each reagent dry out per second, the same as water evaporates.
    /// </summary>
    [DataField]
    public FixedPoint2 Speed = FixedPoint2.New(0.3);

    /// <summary>
    /// When the puddle starts to dry out, set when it appears.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan DryingStart;
}
