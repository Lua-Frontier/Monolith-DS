/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Nebula;

[RegisterComponent]
public sealed partial class NebulaDebrisComponent : Component
{
    [DataField]
    public float Chance = 0.15f;

    [DataField]
    public int MinCount = 1;

    [DataField]
    public int MaxCount = 2;

    [DataField(required: true)]
    public Dictionary<EntProtoId, float> Debris = new();

    [DataField]
    public float TriggerMargin = 600f;

    [DataField]
    public float MinPlayerDistance = 250f;

    [DataField]
    public float Clearance = 64f;

    [DataField]
    public float LeaveMargin = 500f;

    [ViewVariables]
    public bool Active;

    [ViewVariables]
    public List<EntityUid> Spawned = new();
}

[RegisterComponent]
public sealed partial class NebulaDebrisPieceComponent : Component
{
    [ViewVariables]
    public EntityUid Nebula;
}
