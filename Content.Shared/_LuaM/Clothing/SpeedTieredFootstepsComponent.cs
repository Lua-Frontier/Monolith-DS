using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Clothing;

/// <summary>
/// Picks the footstep sound of a suit by how much it slows the wearer down: light, medium or heavy steps.
/// The slowdown is read from the suit's ClothingSpeedModifier, the stronger of walk and sprint counts.
/// On grass, snow, dirt, wood, carpet and water the suit uses steps of the same tier made for that surface.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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

    /// <summary>
    /// The tier picked on map init.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FootstepsTier Tier;

    [DataField]
    public SoundSpecifier LightSound = new SoundCollectionSpecifier("FootstepHardsuitLight");

    [DataField]
    public SoundSpecifier MediumSound = new SoundCollectionSpecifier("FootstepHardsuitMedium");

    [DataField]
    public SoundSpecifier HeavySound = new SoundCollectionSpecifier("FootstepHardsuitHeavy");

    /// <summary>
    /// Steps for non-metal surfaces, keyed by the footstep collection of the tile or of the entity on it (carpets).
    /// Surfaces with any other footsteps keep the suit's own sound.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SoundCollectionPrototype>, TieredFootstepSounds> SurfaceSounds = new()
    {
        ["FootstepGrass"] = new("FootstepHardsuitLightGrass", "FootstepHardsuitMediumGrass", "FootstepHardsuitHeavyGrass"),
        ["FootstepSnow"] = new("FootstepHardsuitLightSnow", "FootstepHardsuitMediumSnow", "FootstepHardsuitHeavySnow"),
        ["FootstepAsteroid"] = new("FootstepHardsuitLightDirt", "FootstepHardsuitMediumDirt", "FootstepHardsuitHeavyDirt"),
        ["FootstepWood"] = new("FootstepHardsuitLightWood", "FootstepHardsuitMediumWood", "FootstepHardsuitHeavyWood"),
        ["FootstepCarpet"] = new("FootstepHardsuitLightCarpet", "FootstepHardsuitMediumCarpet", "FootstepHardsuitHeavyCarpet"),
        ["FootstepWater"] = new("FootstepHardsuitLightWater", "FootstepHardsuitMediumWater", "FootstepHardsuitHeavyWater"),
    };
}

[DataDefinition]
public sealed partial class TieredFootstepSounds
{
    [DataField(required: true)]
    public SoundSpecifier Light = default!;

    [DataField(required: true)]
    public SoundSpecifier Medium = default!;

    [DataField(required: true)]
    public SoundSpecifier Heavy = default!;

    public TieredFootstepSounds()
    {
    }

    public TieredFootstepSounds(string light, string medium, string heavy)
    {
        Light = new SoundCollectionSpecifier(light);
        Medium = new SoundCollectionSpecifier(medium);
        Heavy = new SoundCollectionSpecifier(heavy);
    }

    public SoundSpecifier Get(FootstepsTier tier)
    {
        return tier switch
        {
            FootstepsTier.Heavy => Heavy,
            FootstepsTier.Medium => Medium,
            _ => Light,
        };
    }
}

[Serializable, NetSerializable]
public enum FootstepsTier : byte
{
    Light,
    Medium,
    Heavy,
}
