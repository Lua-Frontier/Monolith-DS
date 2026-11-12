// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Collections.Concurrent;
using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;

namespace Content.Shared._Lua.SpaceHazards;

public static class NebulaVeilHelpers
{
    private const int MaxCachedContours = 4096;

    // The outline depends only on these three, and building it costs thousands of noise samples.
    private static readonly ConcurrentDictionary<(int Seed, float Radius, float Density), Vector2[]> Contours = new();

    /// <summary>
    /// Outline of the dense part of the cloud, relative to the field position.
    /// </summary>
    public static Vector2[] GetMidContour(int seed, float radius, float density)
    {
        var key = (seed, radius, density);
        if (Contours.TryGetValue(key, out var contour))
            return contour;

        if (Contours.Count >= MaxCachedContours)
            Contours.Clear();

        contour = AmbientSpaceNebulaNoise.BuildMidLayerContour(Vector2.Zero, radius, seed, density);
        Contours[key] = contour;
        return contour;
    }

    public static bool IsInMidZone(
        AmbientSpaceFieldComponent field,
        Vector2 fieldPos,
        Vector2 worldPos,
        float? radiusOverride = null)
    {
        var radius = MathF.Max(radiusOverride ?? field.Radius, 1f);
        var delta = worldPos - fieldPos;
        if (delta.LengthSquared() > radius * radius)
            return false;

        return ContainsPoint(GetMidContour(field.Seed, radius, field.Density), delta);
    }

    private static bool ContainsPoint(ReadOnlySpan<Vector2> polygon, Vector2 point)
    {
        if (polygon.Length < 3)
            return false;

        var inside = false;
        var previous = polygon.Length - 1;
        for (var current = 0; current < polygon.Length; current++)
        {
            var a = polygon[current];
            var b = polygon[previous];

            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }
}
