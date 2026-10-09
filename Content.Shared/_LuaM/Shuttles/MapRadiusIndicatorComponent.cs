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
    /// Absolute map position of the circle center. If null, the entity's own position is used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2? Center;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#ccaa55");
}
