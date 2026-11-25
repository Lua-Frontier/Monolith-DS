using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Concussion;

/// <summary>
/// A flashbang: when it goes off, everyone in its range gets their ears ringing, flashed or not, without the blur of a blast.
/// Closer to the flash the ringing is louder and longer.
/// </summary>
[RegisterComponent]
public sealed partial class ConcussiveFlashComponent : Component
{
    /// <summary>
    /// Range of the ringing in tiles, used when the entity has no <c>FlashOnTrigger</c> to take it from.
    /// </summary>
    [DataField]
    public float Range = 7f;

    /// <summary>
    /// Strength right next to the flash, from 0 to 1.
    /// </summary>
    [DataField]
    public float Strength = 0.8f;

    /// <summary>
    /// Part of the strength left at the edge of the flash range.
    /// </summary>
    [DataField]
    public float EdgeStrength = 0.4f;

    /// <summary>
    /// How long a full-strength ringing lasts. Weaker ones are shorter in proportion.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_LuaM/Effects/concussion.ogg");
}
