using System.Numerics;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private const uint AroundColliderTicks = 5;

    private const float AroundColliderMove = 0.05f;

    private readonly Dictionary<EntityUid, AroundColliderEntry> _aroundColliderCache = new();
    private readonly List<EntityUid> _staleAroundCollider = new();
    private GameTick _nextAroundColliderPrune;

    private readonly record struct AroundColliderEntry(MapId Map, Vector2 Position, GameTick Tick, bool Touching);

    private bool IsAroundColliderCached(EntityLookupSystem lookupSystem,
        Entity<PhysicsComponent, MobMoverComponent, TransformComponent> entity)
    {
        if (Timing.InPrediction)
            return IsAroundCollider(lookupSystem, entity);

        var xform = entity.Comp3;
        var position = _transform.GetWorldPosition(xform);
        var now = Timing.CurTick;
        if (_aroundColliderCache.TryGetValue(entity.Owner, out var entry) &&
            entry.Map == xform.MapID &&
            now >= entry.Tick && now.Value - entry.Tick.Value < AroundColliderTicks &&
            (position - entry.Position).LengthSquared() < AroundColliderMove * AroundColliderMove)
        {
            return entry.Touching;
        }

        var touching = IsAroundCollider(lookupSystem, entity);
        _aroundColliderCache[entity.Owner] = new AroundColliderEntry(xform.MapID, position, now, touching);
        PruneAroundCollider(now);
        return touching;
    }

    private void PruneAroundCollider(GameTick now)
    {
        if (now < _nextAroundColliderPrune)
            return;

        _nextAroundColliderPrune = new GameTick(now.Value + 300);
        _staleAroundCollider.Clear();
        foreach (var (uid, entry) in _aroundColliderCache)
        {
            if (entry.Tick > now || now.Value - entry.Tick.Value > AroundColliderTicks * 4)
                _staleAroundCollider.Add(uid);
        }

        foreach (var uid in _staleAroundCollider)
        {
            _aroundColliderCache.Remove(uid);
        }
    }
}
