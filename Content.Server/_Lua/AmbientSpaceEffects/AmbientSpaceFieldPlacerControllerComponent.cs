// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Server._Lua.AmbientSpaceEffects;

/// <summary>
/// Put on a map (through a worldgen config) to scatter nebulae over it once.
/// </summary>
[RegisterComponent]
public sealed partial class AmbientSpaceFieldPlacerControllerComponent : Component
{
    [DataField]
    public int MinCount = 20;

    [DataField]
    public int MaxCount = 30;

    /// <summary>
    /// Nebula centers are picked in a ring between these distances from the map origin.
    /// </summary>
    [DataField]
    public float MinDistance = 2000f;

    [DataField]
    public float MaxDistance = 30000f;

    /// <summary>
    /// Gap between the edge of a nebula and any grid that exists when the nebulae are placed.
    /// </summary>
    [DataField]
    public float MinGridClearance = 1500f;

    /// <summary>
    /// Two nebulae are at least (radius A + radius B) * this apart. Below 1 they may overlap.
    /// </summary>
    [DataField]
    public float MinSpacingFactor = 0.85f;

    [DataField]
    public float MinCenterSeparation = 900f;

    /// <summary>
    /// Share of nebulae that get weather, 0..1.
    /// </summary>
    [DataField]
    public float WeatherChance = 0.24f;

    /// <summary>
    /// Nebulae closer to the map origin than this are always harmless.
    /// </summary>
    [DataField]
    public float MinWeatherDistance;

    [DataField]
    public EntProtoId FieldPrototype = "AmbientSpaceFieldNebula";

    [DataField]
    public bool Spawned;
}
