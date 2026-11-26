// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.MartialArts.Components;

/// <summary>
/// Temporary attack rate, damage and movement speed modifiers from martial arts.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MartialArtModifiersComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<MartialArtModifierData> Data = new();

    /// <summary>
    /// The earliest end time of the modifiers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// (min multiplier, max multiplier, min modifier, max modifier) for every modifier type.
    /// </summary>
    [DataField]
    public Dictionary<MartialArtModifierType, Vector4> MinMaxModifiersMultipliers = new()
    {
        { MartialArtModifierType.AttackRate, new Vector4(0.5f, 4f, -4f, 4f) },
        // Flat negative modifiers are clamped to zero by the damage code
        { MartialArtModifierType.Damage, new Vector4(0.5f, 3f, -20f, 20f) },
        // Flat modifiers are not supported for move speed
        { MartialArtModifierType.MoveSpeed, new Vector4(0.2f, 1.5f, 0f, 0f) },
    };
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MartialArtModifierData
{
    [DataField]
    public MartialArtModifierType Type = MartialArtModifierType.AttackRate;

    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public float Modifier;

    [DataField]
    public TimeSpan EndTime = TimeSpan.Zero;
}

[Flags, Serializable, NetSerializable]
public enum MartialArtModifierType : byte
{
    Invalid = 0,
    AttackRate = 1 << 0,
    Damage = 1 << 1,
    MoveSpeed = 1 << 2,
    Unarmed = 1 << 3,
    Armed = 1 << 4,
    /// <summary>
    /// A slowdown put on someone else, a new one replaces the old one instead of stacking.
    /// </summary>
    Slowdown = 1 << 5,
}
