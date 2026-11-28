using Content.Shared._LuaM.Stacks;
using Content.Shared.Conveyor;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Stacks;

/// <summary>
/// Stacks of materials, ore, produce and glass shards lying loose on the floor pull in nearby stacks of the same kind,
/// so a drill, a self-harvesting plant or a broken window leaves one pile instead of hundreds of entities.
/// </summary>
/// <remarks>
/// Nothing is scanned every tick: a stack is queued when it is spawned, dropped, lands after a throw
/// or comes to rest, and the queue is worked off a few dozen stacks per tick.
/// </remarks>
public sealed class StackMagnetSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    /// <summary>
    /// How far a stack pulls in its neighbours.
    /// </summary>
    private const float Radius = 3f;

    /// <summary>
    /// Gives a batch of freshly spawned stacks (a whole harvest, a drill output) time to land before they merge.
    /// </summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(0.5);

    private const int MaxPerTick = 64;

    /// <summary>
    /// Merges shown as a stack flying into the pile, per pile. The rest just merge.
    /// </summary>
    private const int MaxAnimations = 4;

    /// <summary>
    /// Stacks waiting to pull, ordered by when they are due. A stack queued again just moves its due time
    /// in <see cref="_due"/>; the outdated queue entry is skipped when it comes up.
    /// </summary>
    private readonly PriorityQueue<EntityUid, TimeSpan> _queue = new();
    private readonly Dictionary<EntityUid, TimeSpan> _due = new();
    private readonly Dictionary<EntityUid, TimeSpan> _blocked = new();
    private readonly HashSet<Entity<StackComponent>> _nearby = new();
    private readonly List<Entity<StackComponent>> _group = new();

    private EntityQuery<ConveyedComponent> _conveyedQuery;
    private EntityQuery<MaterialComponent> _materialQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<UnitStackComponent> _unitQuery;
    private EntityQuery<ThrownItemComponent> _thrownQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _conveyedQuery = GetEntityQuery<ConveyedComponent>();
        _materialQuery = GetEntityQuery<MaterialComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _unitQuery = GetEntityQuery<UnitStackComponent>();
        _thrownQuery = GetEntityQuery<ThrownItemComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<StackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StackComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<StackComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<StackComponent, PhysicsSleepEvent>(OnSleep);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _queue.Clear();
        _due.Clear();
        _blocked.Clear();
    }

    private void OnMapInit(Entity<StackComponent> ent, ref MapInitEvent args)
    {
        // Stacks spawned straight into lockers, bags and vendors never get queued.
        if (IsLoose(Transform(ent)))
            Enqueue(ent, SettleDelay);
    }

    private void OnParentChanged(Entity<StackComponent> ent, ref EntParentChangedMessage args)
    {
        if (IsLoose(args.Transform))
            Enqueue(ent, SettleDelay);
    }

    private void OnLand(Entity<StackComponent> ent, ref LandEvent args)
    {
        Enqueue(ent, SettleDelay);
    }

    /// <summary>
    /// A stack came to rest after moving: pushed, pulled or carried by a conveyor to the end of the belt,
    /// where whole lines of ore pile up.
    /// </summary>
    private void OnSleep(Entity<StackComponent> ent, ref PhysicsSleepEvent args)
    {
        if (IsLoose(Transform(ent)))
            Enqueue(ent, SettleDelay);
    }

    /// <summary>
    /// Keeps a stack out of the magnet for a while, e.g. while something is being done to it,
    /// then lets it merge again.
    /// </summary>
    public void Block(EntityUid uid, TimeSpan time)
    {
        _blocked[uid] = _timing.CurTime + time;
        Enqueue(uid, time);
    }

    private void Enqueue(EntityUid uid, TimeSpan delay)
    {
        if (!IsMagnetic(uid))
            return;

        var due = _timing.CurTime + delay;
        if (_due.TryGetValue(uid, out var queued) && queued >= due)
            return;

        _due[uid] = due;
        _queue.Enqueue(uid, due);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var pulled = 0;
        while (pulled < MaxPerTick && _queue.TryPeek(out var uid, out var time) && time <= now)
        {
            _queue.Dequeue();

            // Queued again later; this entry is outdated.
            if (!_due.TryGetValue(uid, out var due) || due != time)
                continue;

            _due.Remove(uid);
            Pull(uid, now);
            pulled++;
        }
    }

    /// <summary>
    /// Merges the stacks of the same kind around <paramref name="uid"/> into as few piles as possible,
    /// the biggest piles taking from the smallest.
    /// </summary>
    private void Pull(EntityUid uid, TimeSpan now)
    {
        if (!TryComp<StackComponent>(uid, out var stack)
            || !_xformQuery.TryComp(uid, out var xform)
            || !CanPull((uid, stack), xform, now))
        {
            if (!Exists(uid) || IsBlockExpired(uid, now))
                _blocked.Remove(uid);
            return;
        }

        _blocked.Remove(uid);

        // A full stack has nothing to take; the stacks around it pull when they are queued themselves.
        if (_stack.GetAvailableSpace(stack) <= 0)
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, Radius, _nearby, LookupFlags.Dynamic | LookupFlags.Sundries);

        _group.Clear();
        _group.Add((uid, stack));
        foreach (var other in _nearby)
        {
            // Cheapest checks first: most neighbours are other kinds of stacks.
            if (other.Owner == uid
                || other.Comp.StackTypeId != stack.StackTypeId
                || _stack.GetAvailableSpace(other.Comp) <= 0
                || !_xformQuery.TryComp(other, out var otherXform)
                || otherXform.ParentUid != xform.ParentUid
                || !CanPull(other, otherXform, now)
                // Last, being a raycast: no pulling through walls, windows or closed doors.
                || !_interaction.InRangeUnobstructed((uid, xform), (other.Owner, otherXform), Radius + 0.1f))
                continue;

            _group.Add(other);
        }

        if (_group.Count < 2)
            return;

        _group.Sort((a, b) => a.Comp.Count != b.Comp.Count
            ? b.Comp.Count.CompareTo(a.Comp.Count)
            : a.Owner.CompareTo(b.Owner));

        var animations = 0;
        for (var d = _group.Count - 1; d > 0; d--)
        {
            var donor = _group[d];
            for (var r = 0; r < d && donor.Comp.Count > 0; r++)
            {
                var recipient = _group[r];
                if (recipient.Comp.Count <= 0 || _stack.GetAvailableSpace(recipient.Comp) <= 0)
                    continue;

                var donorXform = _xformQuery.GetComponent(donor);
                var from = donorXform.Coordinates;
                var rotation = donorXform.LocalRotation;
                if (!_stack.TryAdd(donor, recipient, donor.Comp, recipient.Comp))
                    continue;

                if (donor.Comp.Count <= 0 && animations++ < MaxAnimations)
                    _storage.PlayPickupAnimation(donor, from, _xformQuery.GetComponent(recipient).Coordinates, rotation);
            }
        }
    }

    private bool CanPull(Entity<StackComponent> ent, TransformComponent xform, TimeSpan now)
    {
        if (ent.Comp.Count <= 0
            || ent.Comp.Unlimited
            || ent.Comp.Lingering
            || !IsLoose(xform)
            || !IsMagnetic(ent)
            || _thrownQuery.HasComp(ent) // still flying, it is queued again when it lands
            || IsRidingConveyor(ent) // carried away, it is queued again when it comes to rest
            || TerminatingOrDeleted(ent)
            || EntityManager.IsQueuedForDeletion(ent))
            return false;

        return !_blocked.TryGetValue(ent, out var until) || until <= now;
    }

    /// <summary>
    /// Moving on a running conveyor. Such a stack must neither take a pile lying next to the belt along
    /// (a whole harvest would ride into the grinder at the end of it) nor be pulled off the belt.
    /// A pile resting at the end of the belt is asleep and merges as usual.
    /// </summary>
    private bool IsRidingConveyor(EntityUid uid)
    {
        return _conveyedQuery.HasComp(uid)
            && _physicsQuery.TryComp(uid, out var physics)
            && physics.Awake;
    }

    private bool IsBlockExpired(EntityUid uid, TimeSpan now)
    {
        return _blocked.TryGetValue(uid, out var until) && until <= now;
    }

    /// <summary>
    /// Materials, ore, produce and glass shards. Other stacks (cables, medicine, tiles) are left alone.
    /// </summary>
    private bool IsMagnetic(EntityUid uid)
    {
        return _materialQuery.HasComp(uid) || _unitQuery.HasComp(uid);
    }

    /// <summary>
    /// Lying on the floor or floating in space, not held, stored or anchored.
    /// </summary>
    private static bool IsLoose(TransformComponent xform)
    {
        return !xform.Anchored
            && xform.ParentUid.IsValid()
            && (xform.ParentUid == xform.GridUid || xform.ParentUid == xform.MapUid);
    }
}
