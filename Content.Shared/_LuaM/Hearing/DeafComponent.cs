using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Hearing;

/// <summary>
/// The creature hears no speech and no radio, and every other sound is muffled or gone.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DeafComponent : Component
{
    /// <summary>
    /// How much of the normal volume other sounds keep: 0 is full silence, 1 is untouched.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SoundVolume;
}
