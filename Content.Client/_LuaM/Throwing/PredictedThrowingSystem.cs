using Content.Shared._LuaM.Throwing;
using Robust.Client.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Throwing;

public sealed partial class PredictedThrowingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private static readonly TimeSpan RestTime = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan MaxSettleTime = TimeSpan.FromSeconds(5);
    private const float RestLinearSpeed = 0.01f;
    private const float RestAngularSpeed = 0.05f;

    // -- Предмет после полёта ещё скользит и крутится: пока он не замер, физику продолжаем предсказывать
    private readonly Dictionary<EntityUid, Settling> _settling = new();
    private readonly List<EntityUid> _settled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PhysicsComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
    }

    private void OnUpdateIsPredicted(Entity<PhysicsComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (HasComp<PredictedThrownItemComponent>(ent) || _settling.ContainsKey(ent))
            args.IsPredicted = true;
    }

    private void OnStartup(Entity<PredictedThrownItemComponent> ent, ref ComponentStartup args)
    {
        _settling.Remove(ent);
        _physics.UpdateIsPredicted(ent.Owner);
    }

    private void OnShutdown(Entity<PredictedThrownItemComponent> ent, ref ComponentShutdown args)
    {
        // -- Если выключить предсказание, пока предмет движется, клиент покажет отстающее на пинг серверное состояние: предмет откатится и повторит поворот
        if (TerminatingOrDeleted(ent))
            return;

        _settling[ent] = new Settling(_timing.RealTime);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_settling.Count == 0)
            return;

        var now = _timing.RealTime;
        foreach (var (uid, settling) in _settling)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<PhysicsComponent>(uid, out var physics))
            {
                _settled.Add(uid);
                continue;
            }

            var resting = !physics.Awake
                || physics.LinearVelocity.LengthSquared() < RestLinearSpeed * RestLinearSpeed
                && MathF.Abs(physics.AngularVelocity) < RestAngularSpeed;

            if (!resting)
                settling.RestSince = null;
            else
                settling.RestSince ??= now;

            if (settling.RestSince is { } since && now - since >= RestTime || now - settling.Started >= MaxSettleTime)
                _settled.Add(uid);
        }

        foreach (var uid in _settled)
        {
            _settling.Remove(uid);
            if (!TerminatingOrDeleted(uid))
                _physics.UpdateIsPredicted(uid);
        }

        _settled.Clear();
    }

    private sealed class Settling(TimeSpan started)
    {
        public readonly TimeSpan Started = started;
        public TimeSpan? RestSince;
    }
}
