using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Changes footstep sound
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FootstepModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? FootstepSoundCollection;

    // LuaM-start
    /// <summary>
    /// When several entities on one tile change the footsteps, the highest priority is heard.
    /// Water is below zero, so catwalks, carpets and tables over it are heard instead.
    /// </summary>
    [DataField]
    public int Priority;

    /// <summary>
    /// Heard on a tile also without shoes. Otherwise barefoot steps use the sound of the tile under the entity.
    /// </summary>
    [DataField]
    public bool Barefoot;
    // LuaM-end
}
