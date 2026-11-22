using Content.Shared.Damage;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Staying near furry species builds up an allergic reaction: coughing and watery eyes first,
/// then choking and shock. A mask or a helmet over the mouth stops it from building up.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FurAllergyComponent : Component
{
    /// <summary>
    /// Species whose fur or feathers set the allergy off.
    /// </summary>
    [DataField(required: true)]
    public HashSet<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public float Range = 3f;

    /// <summary>
    /// Seconds of contact, goes up near furries and down away from them or behind a mask.
    /// </summary>
    [DataField]
    public float Exposure;

    /// <summary>
    /// Seconds of contact added at once by hugging a furry or being hugged by one.
    /// </summary>
    [DataField]
    public float HugExposure = 30f;

    [DataField]
    public float MildThreshold = 45f;

    [DataField]
    public float SevereThreshold = 120f;

    /// <summary>
    /// Exposure never goes above this, so walking away always helps in reasonable time.
    /// </summary>
    [DataField]
    public float MaxExposure = 150f;

    /// <summary>
    /// How much exposure goes away every second without contact.
    /// </summary>
    [DataField]
    public float RecoveryRate = 2f;

    [DataField, AutoNetworkedField]
    public FurAllergyStage Stage = FurAllergyStage.None;

    /// <summary>
    /// Watery eyes, the same white haze as poor eyesight.
    /// </summary>
    [DataField]
    public float Blur = 3f;

    [DataField]
    public TimeSpan CoughMin = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan CoughMax = TimeSpan.FromSeconds(20);

    [DataField]
    public DamageSpecifier ChokeDamage = new();

    [DataField]
    public TimeSpan ShockInterval = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ShockDuration = TimeSpan.FromSeconds(3);

    public TimeSpan NextUpdate;
    public TimeSpan NextCough;
    public TimeSpan NextShock;
}

[Serializable, NetSerializable]
public enum FurAllergyStage : byte
{
    None,
    Mild,
    Severe,
}
