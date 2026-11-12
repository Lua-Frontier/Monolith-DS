// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.SpaceHazards;

/// <summary>
/// Put by the server on a grid that sits inside the dense part of a nebula with weather.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NebulaPresenceComponent : Component
{
    [AutoNetworkedField]
    public List<ProtoId<NebulaWeatherPrototype>> ActiveWeathers = new();

    /// <summary>
    /// How deep in the cloud the grid is, 0..1, same order as <see cref="ActiveWeathers"/>.
    /// </summary>
    [AutoNetworkedField]
    public List<float> ActiveIntensities = new();
}

/// <summary>
/// Marks a grid hidden from radars by a veil nebula.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NebulaVeilTrackedComponent : Component;

/// <summary>
/// Anchored on a grid, cancels this share of the nebula thrust penalty for it. The best one on the grid counts.
/// </summary>
[RegisterComponent]
public sealed partial class NebulaThrustResistanceComponent : Component
{
    [DataField]
    public float Resistance = 1f;
}

/// <summary>
/// On a gun, cancels this share of the nebula fire rate penalty.
/// </summary>
[RegisterComponent]
public sealed partial class NebulaWeaponResistanceComponent : Component
{
    [DataField]
    public float Resistance = 1f;
}

/// <summary>
/// A radio with this (or held by / standing on something with this) works through a nebula radio blackout.
/// </summary>
[RegisterComponent]
public sealed partial class NebulaRadioProtectedComponent : Component;
