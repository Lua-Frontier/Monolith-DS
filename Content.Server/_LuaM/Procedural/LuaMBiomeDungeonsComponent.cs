/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Procedural;

[RegisterComponent, Access(typeof(LuaMBiomeDungeonsSystem))]
public sealed partial class LuaMBiomeDungeonsComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<DungeonConfigPrototype>> Configs = new();

    [DataField]
    public int MinCount = 2;

    [DataField]
    public int MaxCount = 4;

    [DataField]
    public int MinDistance = 48;

    [DataField]
    public int MaxDistance = 256;

    [DataField]
    public int Spacing = 96;

    [DataField]
    public Vector2i Origin;

    [DataField]
    public bool Generated;
}
