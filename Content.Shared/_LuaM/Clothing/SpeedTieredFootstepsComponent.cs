using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Clothing;

/// <summary>
/// Picks the footstep sound of a suit by how much it slows the wearer down: light, medium or heavy steps.
/// The slowdown is read from the suit's ClothingSpeedModifier, the stronger of walk and sprint counts.
/// </summary>
[RegisterComponent]
public sealed partial class SpeedTieredFootstepsComponent : Component
{
    /// <summary>
    /// Suits slowing down by more than this get the medium steps.
    /// </summary>
    [DataField]
    public float MediumSlowdown = 0.2f;

    /// <summary>
    /// Suits slowing down by more than this get the heavy steps.
    /// </summary>
    [DataField]
    public float HeavySlowdown = 0.35f;

    /// <summary>
    /// Ignores the slowdown and always uses this tier, for suits that should sound heavier or lighter than they are.
    /// </summary>
    [DataField]
    public FootstepsTier? ForcedTier;

    [DataField]
    public SoundSpecifier LightSound = new SoundCollectionSpecifier("FootstepHardsuitLight");

    [DataField]
    public SoundSpecifier MediumSound = new SoundCollectionSpecifier("FootstepHardsuitMedium");

    [DataField]
    public SoundSpecifier HeavySound = new SoundCollectionSpecifier("FootstepHardsuitHeavy");
}

public enum FootstepsTier : byte
{
    Light,
    Medium,
    Heavy,
}
