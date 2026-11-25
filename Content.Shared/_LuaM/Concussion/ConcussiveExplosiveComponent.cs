using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Concussion;

/// <summary>
/// The explosion of this explosive concusses players caught in it: their ears ring and their sight blurs for a while.
/// Only the area the explosion damages counts, not the visual shockwave. Strength follows the size of the explosion,
/// so one component fits a grenade and a ship shell.
/// </summary>
[RegisterComponent]
public sealed partial class ConcussiveExplosiveComponent : Component
{
    /// <summary>
    /// Blast radius in tiles, instead of the one worked out from the explosion.
    /// Needed by blasts without an explosion of their own, like a shrapnel grenade: they concuss when triggered.
    /// </summary>
    [DataField]
    public float? Radius;

    /// <summary>
    /// Concussion range per tile of explosion radius.
    /// </summary>
    [DataField]
    public float RangePerRadius = 1f;

    /// <summary>
    /// Tiles added to the concussion range on top of <see cref="RangePerRadius"/>, so the edge tiles of the explosion count.
    /// </summary>
    [DataField]
    public float RangeBonus = 0.5f;

    /// <summary>
    /// Part of the strength left at the edge of the range: anyone caught in the blast is properly concussed.
    /// </summary>
    [DataField]
    public float EdgeStrength = 0.4f;

    [DataField]
    public float MaxRange = 20f;

    /// <summary>
    /// Explosion radius at which the concussion reaches full strength at the epicenter.
    /// </summary>
    [DataField]
    public float FullPowerRadius = 4f;

    /// <summary>
    /// Strength at the epicenter of the smallest explosions.
    /// </summary>
    [DataField]
    public float MinPower = 0.2f;

    [DataField]
    public float PowerMultiplier = 1f;

    /// <summary>
    /// Weaker concussions than this are skipped.
    /// </summary>
    [DataField]
    public float MinStrength = 0.05f;

    /// <summary>
    /// How long a full-strength concussion lasts. Weaker ones are shorter in proportion.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_LuaM/Effects/concussion.ogg");

    /// <summary>
    /// Inner part of the range, from 0 to 1, that counts as the epicenter: players there are also deafened for a while.
    /// 0 turns the deafness off.
    /// </summary>
    [DataField]
    public float DeafRangeFraction = 0.4f;

    /// <summary>
    /// How long the deafness lasts at full strength. Weaker blasts deafen for less.
    /// </summary>
    [DataField]
    public TimeSpan DeafMaxDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan DeafMinDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How much of the normal volume other sounds keep while deafened.
    /// </summary>
    [DataField]
    public float DeafSoundVolume = 0.25f;
}
