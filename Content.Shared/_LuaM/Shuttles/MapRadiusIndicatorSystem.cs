using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Shared._LuaM.Shuttles;

/// <summary>
/// Shared helpers for drawing <see cref="MapRadiusIndicatorComponent"/> circles on shuttle consoles.
/// </summary>
public sealed partial class MapRadiusIndicatorSystem : EntitySystem
{
    [Dependency] private RadarStealthFieldSystem _stealthField = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// World position of the circle center: the set center, or the center of mass of the entity, so the circle
    /// sits on the station and not on the grid origin.
    /// </summary>
    public Vector2 GetCenter(EntityUid uid, MapRadiusIndicatorComponent indicator, TransformComponent xform)
    {
        if (indicator.Center is { } center)
            return center;

        var localCenter = TryComp<PhysicsComponent>(uid, out var physics) ? physics.LocalCenter : Vector2.Zero;
        return Vector2.Transform(localCenter, _transform.GetWorldMatrix(xform));
    }

    /// <summary>
    /// Whether the local player looking from the given position must not see the circle.
    /// </summary>
    public bool IsHiddenFromLocalPlayer(EntityUid uid, MapRadiusIndicatorComponent indicator, MapCoordinates viewer)
    {
        return indicator.HideWithRadarStealth
               && HasComp<RadarStealthFieldComponent>(uid)
               && _stealthField.IsHiddenFromLocalPlayer(uid, viewer);
    }
}
