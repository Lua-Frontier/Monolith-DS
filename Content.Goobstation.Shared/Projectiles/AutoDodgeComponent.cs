// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Goobstation.Shared.Projectiles;

/// <summary>
/// Passively dodges incoming attacks instead of being hit by them,
/// playing a dodge animation on the entity's sprite.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AutoDodgeComponent : Component
{
    /// <summary>
    /// Whether ranged attacks (projectiles and hitscan) are dodged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DodgeRanged = true;

    /// <summary>
    /// Whether melee attacks are dodged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DodgeMelee = true;

    [DataField]
    public SoundSpecifier? DodgeSound = new SoundCollectionSpecifier("BulletMiss", AudioParams.Default.WithVariation(0.125f));

    [DataField]
    public string AnimationKey = "auto-dodge";

    [DataField]
    public float SidestepDistance = 0.3f;

    [DataField]
    public float LeanDegrees = 28f;

    [DataField]
    public TimeSpan AnimationLength = TimeSpan.FromSeconds(0.35);

    /// <summary>
    /// Minimum time between dodge effects.
    /// </summary>
    [DataField]
    public TimeSpan EffectCooldown = TimeSpan.FromSeconds(0.05);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextEffectTime;
}

/// <summary>
/// Sent to nearby clients when an entity auto-dodges so they play the dodge visuals.
/// </summary>
[Serializable, NetSerializable]
public sealed class AutoDodgeEffectEvent(NetEntity entity, Vector2 direction) : EntityEventArgs
{
    public NetEntity Entity = entity;

    public Vector2 Direction = direction;
}
