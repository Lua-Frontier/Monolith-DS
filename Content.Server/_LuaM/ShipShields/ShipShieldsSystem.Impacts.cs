/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.ShipShields;
using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Player;

namespace Content.Server._Crescent.ShipShields;

public sealed partial class ShipShieldsSystem
{
    private const float ImpactMinStrength = 0.35f;
    private const float ImpactMaxStrength = 1.5f;
    private const float ImpactDamagePerStrength = 150f;

    private void RaiseProjectileImpact(EntityUid shield, EntityUid projectile, ProjectileComponent component)
    {
        if (!TryGetShieldFrame(shield, out var position, out var rotation, out var halfSize))
            return;

        var damage = (float) component.Damage.GetTotal();
        if (TryComp<ExplosiveComponent>(projectile, out var explosive) && _prototypeManager.TryIndex(explosive.ExplosionType, out var type))
            damage += explosive.TotalIntensity * (float) type.DamagePerIntensity.GetTotal();

        var local = (-rotation).RotateVec(_transformSystem.GetWorldPosition(projectile) - position);
        RaiseImpact(shield, ProjectOnBorder(local, halfSize), damage);
    }

    private void RaiseHitscanImpact(EntityUid shield, EntityCoordinates from, Vector2 direction, float damage)
    {
        if (!TryGetShieldFrame(shield, out var position, out var rotation, out var halfSize))
            return;

        var origin = (-rotation).RotateVec(_transformSystem.ToMapCoordinates(from).Position - position);
        var dir = (-rotation).RotateVec(direction);
        RaiseImpact(shield, RayBorderHit(origin, dir, halfSize) ?? ProjectOnBorder(origin, halfSize), damage);
    }

    private void RaiseImpact(EntityUid shield, Vector2 local, float damage)
    {
        var strength = Math.Clamp(ImpactMinStrength + damage / ImpactDamagePerStrength, ImpactMinStrength, ImpactMaxStrength);
        var (position, rotation) = _transformSystem.GetWorldPositionRotation(shield);
        var world = new MapCoordinates(position + rotation.RotateVec(local), Transform(shield).MapID);
        RaiseNetworkEvent(new ShipShieldImpactEvent(GetNetEntity(shield), local, strength), Filter.Pvs(world));
    }

    private bool TryGetShieldFrame(EntityUid shield, out Vector2 position, out Angle rotation, out Vector2 halfSize)
    {
        (position, rotation) = _transformSystem.GetWorldPositionRotation(shield);
        halfSize = Vector2.Zero;
        if (_fixtureSystem.GetFixtureOrNull(shield, "shield") is not { Shape: ChainShape chain } || chain.Count < 2)
            return false;

        foreach (var vertex in chain.Vertices)
            halfSize = Vector2.Max(halfSize, Vector2.Abs(vertex));

        return halfSize.X > 0f && halfSize.Y > 0f;
    }

    private static Vector2 ProjectOnBorder(Vector2 local, Vector2 halfSize)
    {
        var scaled = local / halfSize;
        var length = scaled.Length();
        return length < 0.0001f ? new Vector2(halfSize.X, 0f) : local / length;
    }

    private static Vector2? RayBorderHit(Vector2 origin, Vector2 direction, Vector2 halfSize)
    {
        var o = origin / halfSize;
        var d = direction / halfSize;
        var a = Vector2.Dot(d, d);
        if (a < 0.000001f)
            return null;

        var b = 2f * Vector2.Dot(o, d);
        var c = Vector2.Dot(o, o) - 1f;
        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return null;

        var root = MathF.Sqrt(discriminant);
        var t = (-b - root) / (2f * a);
        if (t < 0f)
            t = (-b + root) / (2f * a);

        return t < 0f ? null : origin + direction * t;
    }
}
