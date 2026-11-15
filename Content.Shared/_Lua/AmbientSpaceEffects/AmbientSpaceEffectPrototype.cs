// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.AmbientSpaceEffects;

[Prototype]
public sealed partial class AmbientSpaceEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Shader { get; private set; } = "AmbientNebula";

    [DataField]
    public float LowerOpacity { get; private set; } = 0.07f;

    [DataField]
    public float MidOpacity { get; private set; } = 0.16f;

    [DataField]
    public float UpperOpacity { get; private set; } = 0.08f;

    [DataField]
    public float MaxOpacity { get; private set; } = 0.3f;

    [DataField]
    public float LowerParallax { get; private set; } = 0.85f;

    [DataField]
    public float UpperParallax { get; private set; } = 1.15f;

    [DataField]
    public float FlowSpeed { get; private set; } = 0.2f;
}
