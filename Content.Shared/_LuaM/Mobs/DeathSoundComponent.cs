using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Mobs;

/// <summary>
/// Plays a sound when the mob dies, whatever killed it.
/// </summary>
[RegisterComponent]
public sealed partial class DeathSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}
