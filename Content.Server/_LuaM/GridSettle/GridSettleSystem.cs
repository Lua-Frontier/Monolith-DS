using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Ghost;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.GridSettle;

public sealed partial class GridSettleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public const float FrictionMultiplier = 10f;

    public const float SleepSpeed = 0.1f;

    public const float SleepSpin = 0.02f;

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextScan;
    private readonly HashSet<EntityUid> _activeGrids = new();
    private readonly HashSet<EntityUid> _group = new();
    private readonly Stack<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GridSettleComponent, TileFrictionEvent>(OnFriction);
    }

    private void OnFriction(Entity<GridSettleComponent> ent, ref TileFrictionEvent args)
    {
        args.Modifier *= FrictionMultiplier;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextScan)
            return;

        _nextScan = _timing.CurTime + ScanInterval;
        CollectActiveGrids();

        var query = EntityQueryEnumerator<ShuttleComponent, MapGridComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var body))
        {
            if (body.BodyType != BodyType.Dynamic || HasComp<FTLComponent>(uid) || HasComp<CEZGridFallerComponent>(uid) ||
                !IsIdle(uid))
            {
                RemComp<GridSettleComponent>(uid);
                continue;
            }

            EnsureComp<GridSettleComponent>(uid);
            if (body.Awake && body.LinearVelocity.Length() < SleepSpeed && MathF.Abs(body.AngularVelocity) < SleepSpin)
                _physics.SetAwake((uid, body), false);
        }
    }

    private void CollectActiveGrids()
    {
        _activeGrids.Clear();

        var actors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is { } grid && !HasComp<GhostComponent>(uid))
                _activeGrids.Add(grid);
        }

        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var console, out var xform))
        {
            if (console.SubscribedPilots.Count > 0 && xform.GridUid is { } grid)
                _activeGrids.Add(grid);
        }
    }

    private bool IsIdle(EntityUid grid)
    {
        _group.Clear();
        _pending.Clear();
        _pending.Push(grid);
        _group.Add(grid);

        while (_pending.TryPop(out var current))
        {
            if (_activeGrids.Contains(current) || IsThrusting(current))
                return false;

            if (!TryComp<JointComponent>(current, out var joints))
                continue;

            foreach (var joint in joints.GetJoints.Values)
            {
                var other = joint.BodyAUid == current ? joint.BodyBUid : joint.BodyAUid;
                if (Transform(other).GridUid is { } otherGrid && _group.Add(otherGrid))
                    _pending.Push(otherGrid);
            }
        }

        return true;
    }

    private bool IsThrusting(EntityUid grid)
    {
        if (!TryComp<ShuttleComponent>(grid, out var shuttle))
            return false;

        if (shuttle.ThrustDirections != DirectionFlag.None)
            return true;

        foreach (var thruster in shuttle.AngularThrusters)
        {
            if (TryComp<ThrusterComponent>(thruster, out var comp) && comp.Firing)
                return true;
        }

        return false;
    }
}
