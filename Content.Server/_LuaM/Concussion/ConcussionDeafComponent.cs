using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._LuaM.Concussion;

/// <summary>
/// The creature was in the epicenter of a blast and is deaf until <see cref="EndTime"/>.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(ConcussionSystem))]
public sealed partial class ConcussionDeafComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndTime;
}
