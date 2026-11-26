// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Grab;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.GrabIntent;

/// <summary>
/// The entity can tighten its pull into a grab: soft, hard and a choke.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrabIntentComponent : Component
{
    [DataField, AutoNetworkedField]
    public GrabStage GrabStage = GrabStage.No;

    /// <summary>
    /// A grab stage change takes the next attack, this is the delay it adds.
    /// </summary>
    [DataField]
    public TimeSpan StageChangeCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Base chance to break free from every stage.
    /// </summary>
    [DataField]
    public Dictionary<GrabStage, float> EscapeChances = new()
    {
        { GrabStage.No, 1f },
        { GrabStage.Soft, 1f },
        { GrabStage.Hard, 0.6f },
        { GrabStage.Suffocate, 0.2f },
    };

    [DataField]
    public float DownedEscapeChanceMultiplier = 0.5f;

    /// <summary>
    /// Stamina damage of tightening a choke even more.
    /// </summary>
    [DataField]
    public float SuffocateGrabStaminaDamage = 10f;

    [DataField]
    public float SoftGrabSpeedModifier = 0.9f;

    [DataField]
    public float HardGrabSpeedModifier = 0.7f;

    [DataField]
    public float ChokeGrabSpeedModifier = 0.4f;

    /// <summary>
    /// Damage the thrown one takes when the throw ends.
    /// </summary>
    [DataField]
    public DamageSpecifier GrabThrowDamage = new()
    {
        DamageDict = { { "Blunt", 10 } },
    };

    [DataField]
    public float GrabThrownSpeed = 7f;

    [DataField]
    public float ThrowingDistance = 4f;

    [DataField]
    public TimeSpan ThrowCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier GrabSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public GrabStage TableSlamRequiredStage = GrabStage.Hard;

    [DataField]
    public float TableSlamRange = 2f;

    [DataField]
    public TimeSpan TableSlamCooldown = TimeSpan.FromSeconds(3);
}
