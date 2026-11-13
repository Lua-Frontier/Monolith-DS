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

    [DataField]
    public float MinDistance = 1000f;

    [DataField]
    public float MaxDistance = 28000f;

    [DataField]
    public float Spacing = 200f;

    [DataField]
    public float DangerChance = 0.1f;

    [DataField]
    public float DangerClearance = 500f;

    [DataField]
    public EntProtoId FieldPrototype = "AmbientSpaceFieldNebula";

    [DataField]
    public bool Spawned;
}
