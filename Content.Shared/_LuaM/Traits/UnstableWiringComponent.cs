using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// IPC wiring that shorts out from shock damage or a stamina crit: the IPC is stunned, loses battery charge
/// and lets out a discharge that hurts itself and everyone around.
/// </summary>
[RegisterComponent]
public sealed partial class UnstableWiringComponent : Component
{
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Part of the max battery charge lost on a short.
    /// </summary>
    [DataField]
    public float BatteryLoss = 0.25f;

    [DataField]
    public float Radius = 2.5f;

    /// <summary>
    /// Damage of the discharge, to the IPC itself and to everyone in the radius.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public EntProtoId Effect = "EffectSparks";

    [DataField]
    public LocId Popup = "unstable-wiring-short";

    [ViewVariables]
    public TimeSpan NextShort;

    /// <summary>
    /// Whether the IPC was in stamina crit on the last check, so only entering it shorts the wiring.
    /// </summary>
    [ViewVariables]
    public bool WasStaminaCrit;
}
