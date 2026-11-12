// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.AmbientSpaceEffects;

/// <summary>
/// A nebula cloud hanging in space. The shape is fully derived from <see cref="Seed"/>, <see cref="Radius"/> and
/// <see cref="Density"/>, so the server and every client agree on it without sending the outline.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmbientSpaceFieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AmbientSpaceEffectPrototype> Effect = "Nebula";

    [DataField, AutoNetworkedField]
    public float Radius = 250f;

    [DataField, AutoNetworkedField]
    public int Seed;

    [DataField, AutoNetworkedField]
    public float Density = 0.55f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#5AD0FF").WithAlpha(0.85f);

    /// <summary>
    /// Weathers active inside the dense (mid) part of the cloud. Empty means a harmless cloud.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<NebulaWeatherPrototype>> Weathers = new();

    public bool HasWeather => Weathers.Count > 0;
}
