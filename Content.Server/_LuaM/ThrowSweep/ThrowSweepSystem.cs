using System.Numerics;
using Content.Shared._LuaM.Performance;
using Content.Shared.Throwing;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server._LuaM.ThrowSweep;

public sealed class ThrowSweepSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private RayCastSystem _rayCast = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float Gap = 0.02f;

    private const float Bounce = 0.1f;

    private float _sweepSpeed;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    private readonly List<(EntityUid Uid, EntityUid Parent, Vector2 LocalStart)> _tracked = new();

    public override void Initialize()
    {
        base.Initialize();

        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);

        Subs.CVar(_cfg, PerformanceCVars.ThrowSweepSpeed, value => _sweepSpeed = value, true);
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent args)
    {
        _tracked.Clear();

        if (_sweepSpeed <= 0f)
            return;

        var query = EntityQueryEnumerator<ThrownItemComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var body, out var xform))
        {
            if (body.BodyType == BodyType.Static || !body.CanCollide || xform.MapUid == null || !xform.ParentUid.IsValid())
                continue;

            if (body.LinearVelocity.LengthSquared() <= _sweepSpeed * _sweepSpeed)
                continue;

            _tracked.Add((uid, xform.ParentUid, xform.LocalPosition));
        }
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent args)
    {
        foreach (var (uid, parent, localStart) in _tracked)
        {
            if (TerminatingOrDeleted(uid)
                || !TryComp(uid, out TransformComponent? xform)
                || xform.ParentUid != parent
                || !_physicsQuery.TryComp(uid, out var body)
                || !_fixturesQuery.TryComp(uid, out var fixtures)
                || !TryGetHardFixture(fixtures, out var ourFixture, out var mask))
            {
                continue;
            }

            var parentMatrix = _transform.GetWorldMatrix(parent);
            var start = Vector2.Transform(localStart, parentMatrix);
            var end = _transform.GetWorldPosition(xform);
            var travel = end - start;
            var distance = travel.Length();
            if (distance <= 0f)
                continue;

            var direction = travel / distance;

            // -- IntersectRay проверяет только AABB фикстур: диагональная стена занимает весь тайл, и бросок в её пустую
            // -- половину откатывался назад без удара, а клиент честно отскакивал от скоса. Луч по форме фикстур
            var filter = new QueryFilter
            {
                MaskBits = mask,
                Flags = QueryFlags.Static,
                IsIgnored = other => !IsWall(uid, body, ourFixture, other),
            };

            var result = _rayCast.CastRayClosest(xform.MapID, start, travel, filter);
            if (!result.Hit)
                continue;

            var nearest = result.Results[0].Fraction * distance;

            var aabb = _lookup.GetWorldAABB(uid, xform);
            var half = aabb.Size / 2f;
            var reach = MathF.Abs(direction.X) * half.X + MathF.Abs(direction.Y) * half.Y;
            var back = MathF.Max(0f, nearest - reach - Gap);

            _transform.SetWorldPosition((uid, xform), start + direction * back);
            _physics.SetLinearVelocity(uid, -body.LinearVelocity * Bounce, body: body);
        }

        _tracked.Clear();
    }

    private bool IsWall(EntityUid uid, PhysicsComponent body, Fixture ourFixture, EntityUid other)
    {
        if (other == uid || _gridQuery.HasComp(other))
            return false;

        if (!_physicsQuery.TryComp(other, out var otherBody) || otherBody.BodyType != BodyType.Static || !otherBody.CanCollide)
            return false;

        if (!_fixturesQuery.TryComp(other, out var otherFixtures) || !TryGetHardFixture(otherFixtures, out var otherFixture, out _))
            return false;

        var ours = new PreventCollideEvent(uid, other, body, otherBody, ourFixture, otherFixture);
        RaiseLocalEvent(uid, ref ours);
        if (ours.Cancelled)
            return false;

        var theirs = new PreventCollideEvent(other, uid, otherBody, body, otherFixture, ourFixture);
        RaiseLocalEvent(other, ref theirs);
        return !theirs.Cancelled;
    }

    private static bool TryGetHardFixture(FixturesComponent fixtures, out Fixture fixture, out int mask)
    {
        fixture = default!;
        mask = 0;
        var found = false;

        foreach (var candidate in fixtures.Fixtures.Values)
        {
            if (!candidate.Hard)
                continue;

            if (!found)
                fixture = candidate;

            found = true;
            mask |= candidate.CollisionMask;
        }

        return found;
    }
}
