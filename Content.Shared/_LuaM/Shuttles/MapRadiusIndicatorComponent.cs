using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Shuttles;

/// <summary>
/// Draws a circle on the shuttle console map, e.g. to show the area where an asteroid cluster spawns.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MapRadiusIndicatorComponent : Component
{
    /// <summary>
    /// Radius of the circle in meters.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public float Radius;

    /// <summary>
    /// Absolute map position of the circle center. If null, the entity's own center is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2? Center;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#ccaa55");

    /// <summary>
    /// Color of the outline, e.g. for a station shared by two factions. If null, <see cref="Color"/> is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? OutlineColor;

    /// <summary>
    /// Opacity of the filled circle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FillAlpha = 0.03f;

    /// <summary>
    /// Opacity of the circle outline.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OutlineAlpha = 0.3f;

    /// <summary>
    /// Hide the circle whenever the entity itself is hidden by its <see cref="RadarStealthFieldComponent"/>,
    /// so it does not give away where a hidden outpost is.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HideWithRadarStealth;
}
