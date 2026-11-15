/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Server._Mono.NPC.HTN;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Nebula;

// -- Корабли облетают только опасные туманности; безопасные не учитываются вовсе
public sealed partial class NebulaAvoidanceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float Margin = 80f;
    private const float CircleExtra = 60f;
    private const float SpawnMargin = 100f;
    private const float MinReach = 600f;
    private const float OrbitOutward = 0.35f;
    private const float StopStep = 16f;
    private const float StopClearance = 30f;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    private readonly List<Hazard> _hazards = new();

    private readonly record struct Hazard(EntityUid Uid, Vector2 Center, Vector2[] Contour, float Inflate, float CircleRadius);

    public void Refresh(ShipSteererComponent steerer, MapCoordinates ship, MapCoordinates target, float shipRadius)
    {
        var now = _timing.CurTime;
        if (now < steerer.NextNebulaCheck)
            return;

        steerer.NextNebulaCheck = now + CheckInterval;
        steerer.NebulaStopPoint = null;
        steerer.NebulaDetour = null;

        if (ship.MapId != target.MapId)
            return;

        CollectHazards(ship.MapId, ship.Position, target.Position, shipRadius);
        if (_hazards.Count == 0)
        {
            steerer.NebulaDetourField = null;
            return;
        }

        var goal = target.Position;
        if (steerer.StopAtNebulae && FindStopPoint(ship.Position, goal) is { } stop)
        {
            steerer.NebulaStopPoint = stop;
            goal = stop;
        }

        steerer.NebulaDetour = FindDetour(steerer, ship.Position, goal);
    }

    public bool IsInHazard(MapCoordinates coordinates, float margin = SpawnMargin)
    {
        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var field, out var xform))
        {
            if (!field.HasWeather || xform.MapID != coordinates.MapId || field.Seed == 0)
                continue;

            var radius = MaxLength(GetContour(field)) + margin;
            if (Vector2.DistanceSquared(_transform.GetWorldPosition(xform), coordinates.Position) <= radius * radius)
                return true;
        }

        return false;
    }

    private void CollectHazards(MapId map, Vector2 from, Vector2 to, float shipRadius)
    {
        _hazards.Clear();
        var bounds = new Box2(Vector2.Min(from, to), Vector2.Max(from, to));
        var inflate = Margin + shipRadius;

        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (!field.HasWeather || xform.MapID != map || field.Seed == 0)
                continue;

            var contour = GetContour(field);
            if (contour.Length < 3)
                continue;

            var circle = MaxLength(contour) + inflate + CircleExtra;
            var center = _transform.GetWorldPosition(xform);
            if (!bounds.Enlarged(circle).Contains(center))
                continue;

            _hazards.Add(new Hazard(uid, center, contour, inflate, circle));
        }
    }

    // -- Метка внутри опасной туманности: автопилот встаёт у её края со стороны корабля
    private Vector2? FindStopPoint(Vector2 from, Vector2 to)
    {
        foreach (var hazard in _hazards)
        {
            if (!Inside(hazard, to - hazard.Center) || Inside(hazard, from - hazard.Center))
                continue;

            var direction = from - to;
            var length = direction.Length();
            direction /= length;
            for (var step = StopStep; step < length; step += StopStep)
            {
                var point = to + direction * step;
                if (!Inside(hazard, point - hazard.Center))
                    return point + direction * StopClearance;
            }

            return null;
        }

        return null;
    }

    private Vector2? FindDetour(ShipSteererComponent steerer, Vector2 from, Vector2 to)
    {
        foreach (var hazard in _hazards)
        {
            var relative = from - hazard.Center;
            if (!Inside(hazard, relative) || Inside(hazard, to - hazard.Center))
                continue;

            // -- Корабль уже в опасной зоне: сначала кратчайший выход наружу
            var outward = relative.LengthSquared() > 1f ? Vector2.Normalize(relative) : Vector2.Normalize(to - from);
            steerer.NebulaDetourField = null;
            return from + outward * MathF.Max(hazard.CircleRadius - relative.Length(), MinReach);
        }

        if (FirstBlocking(from, to, null) is not { } blocking)
        {
            steerer.NebulaDetourField = null;
            return null;
        }

        var side = steerer.NebulaDetourField == blocking.Uid
            ? steerer.NebulaDetourSide
            : Cross(blocking.Center - from, to - from) > 0f ? 1 : -1;

        var waypoint = Waypoint(blocking, from, side);
        if (FirstBlocking(from, waypoint, blocking.Uid) != null)
        {
            var other = Waypoint(blocking, from, -side);
            if (FirstBlocking(from, other, blocking.Uid) == null)
            {
                side = -side;
                waypoint = other;
            }
        }

        steerer.NebulaDetourField = blocking.Uid;
        steerer.NebulaDetourSide = side;
        return waypoint;
    }

    private Hazard? FirstBlocking(Vector2 from, Vector2 to, EntityUid? ignore)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared < 1f)
            return null;

        Hazard? best = null;
        var bestT = float.MaxValue;
        foreach (var hazard in _hazards)
        {
            if (hazard.Uid == ignore || Inside(hazard, to - hazard.Center))
                continue;

            var t = Vector2.Dot(hazard.Center - from, segment) / lengthSquared;
            if (t >= bestT || !SegmentHits(hazard, from - hazard.Center, to - hazard.Center))
                continue;

            bestT = t;
            best = hazard;
        }

        return best;
    }

    // -- Снаружи круга: касательная к нему. Внутри круга, но вне облака: вдоль края с уводом наружу.
    // -- Точка ставится далеко, иначе корабль начинает тормозить перед ней
    private static Vector2 Waypoint(Hazard hazard, Vector2 from, int side)
    {
        var toCenter = hazard.Center - from;
        var distance = toCenter.Length();
        var radius = hazard.CircleRadius;

        if (distance > radius + 1f)
        {
            var angle = MathF.Asin(radius / distance) * side;
            var reach = MathF.Max(MathF.Sqrt(distance * distance - radius * radius) * 1.5f, MinReach);
            return from + new Angle(angle).RotateVec(toCenter / distance) * reach;
        }

        var outward = distance > 1f ? -toCenter / distance : Vector2.UnitX;
        var tangent = side > 0 ? new Vector2(outward.Y, -outward.X) : new Vector2(-outward.Y, outward.X);
        return from + Vector2.Normalize(tangent + outward * OrbitOutward) * MinReach;
    }

    private static bool SegmentHits(Hazard hazard, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var t = Math.Clamp(Vector2.Dot(-a, segment) / MathF.Max(segment.LengthSquared(), 0.001f), 0f, 1f);
        if ((a + segment * t).LengthSquared() > hazard.CircleRadius * hazard.CircleRadius)
            return false;

        var contour = hazard.Contour;
        var previous = Inflated(hazard, contour.Length - 1);
        for (var i = 0; i < contour.Length; i++)
        {
            var current = Inflated(hazard, i);
            if (SegmentsIntersect(a, b, previous, current))
                return true;

            previous = current;
        }

        return false;
    }

    private static bool Inside(Hazard hazard, Vector2 point)
    {
        if (point.LengthSquared() > hazard.CircleRadius * hazard.CircleRadius)
            return false;

        var inside = false;
        var contour = hazard.Contour;
        var previous = Inflated(hazard, contour.Length - 1);
        for (var i = 0; i < contour.Length; i++)
        {
            var current = Inflated(hazard, i);
            if ((current.Y > point.Y) != (previous.Y > point.Y) &&
                point.X < (previous.X - current.X) * (point.Y - current.Y) / (previous.Y - current.Y) + current.X)
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }

    // -- Контур звездчатый относительно центра, поэтому запас добавляется вдоль луча
    private static Vector2 Inflated(Hazard hazard, int index)
    {
        var point = hazard.Contour[index];
        var length = point.Length();
        return length > 0.01f ? point * ((length + hazard.Inflate) / length) : point;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var d1 = Cross(q2 - q1, p1 - q1);
        var d2 = Cross(q2 - q1, p2 - q1);
        var d3 = Cross(p2 - p1, q1 - p1);
        var d4 = Cross(p2 - p1, q2 - p1);
        return d1 > 0f != d2 > 0f && d3 > 0f != d4 > 0f;
    }

    private static Vector2[] GetContour(AmbientSpaceFieldComponent field)
    {
        return NebulaVeilHelpers.GetMidContour(field.Seed, MathF.Max(field.Radius, 1f), field.Density);
    }

    private static float MaxLength(Vector2[] contour)
    {
        var max = 0f;
        foreach (var point in contour)
        {
            max = MathF.Max(max, point.LengthSquared());
        }

        return MathF.Sqrt(max);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }
}
