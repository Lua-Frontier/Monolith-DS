/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Server._NF.Worldgen.Components.Debris;
using Content.Server.Worldgen.Systems.GC;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Ghost;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Nebula;

// -- Как обычная генерация по чанкам: обломки разыгрываются, когда игрок подлетает к туманности, и уходят в GC, когда все улетели
public sealed partial class NebulaDebrisSystem : EntitySystem
{
    [Dependency] private GCQueueSystem _gc = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private const int PointAttempts = 24;

    private TimeSpan _nextCheck;
    private readonly List<(MapId Map, Vector2 Pos)> _players = new();
    private List<Entity<MapGridComponent>> _grids = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NebulaDebrisPieceComponent, TryCancelGC>(OnTryCancelGC);
    }

    private void OnTryCancelGC(Entity<NebulaDebrisPieceComponent> ent, ref TryCancelGC args)
    {
        args.Cancelled |= TryComp<NebulaDebrisComponent>(ent.Comp.Nebula, out var debris) && debris.Active;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        CollectPlayers();

        var query = EntityQueryEnumerator<NebulaDebrisComponent, AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var debris, out var field, out var xform))
        {
            if (field.Seed == 0 || xform.MapUid is not { } mapUid)
                continue;

            var center = _transform.GetWorldPosition(xform);
            if (!debris.Active)
            {
                if (_players.Count > 0 && AnyPlayerWithin(xform.MapID, center, field.Radius + debris.TriggerMargin))
                    Activate((uid, debris), field, xform.MapID, mapUid, center);

                continue;
            }

            if (!AnyPlayerWithin(xform.MapID, center, field.Radius + debris.TriggerMargin + debris.LeaveMargin))
                Deactivate(debris);
        }
    }

    private void Activate(Entity<NebulaDebrisComponent> ent, AmbientSpaceFieldComponent field, MapId map, EntityUid mapUid, Vector2 center)
    {
        var debris = ent.Comp;
        debris.Active = true;

        // -- Обломки, которые GC ещё не успел удалить, остаются: новые поверх них не разыгрываются
        debris.Spawned.RemoveAll(e => TerminatingOrDeleted(e));
        if (debris.Spawned.Count > 0 || debris.Debris.Count == 0 || !_random.Prob(debris.Chance))
            return;

        var count = _random.Next(debris.MinCount, debris.MaxCount + 1);
        for (var i = 0; i < count; i++)
        {
            if (!TryPickPoint(field, debris, map, center, out var point))
                continue;

            var piece = Spawn(PickDebris(debris), new EntityCoordinates(mapUid, point));
            EnsureComp<SpaceDebrisComponent>(piece);
            EnsureComp<NebulaDebrisPieceComponent>(piece).Nebula = ent;
            debris.Spawned.Add(piece);
        }
    }

    private void Deactivate(NebulaDebrisComponent debris)
    {
        debris.Active = false;
        foreach (var piece in debris.Spawned)
        {
            if (!TerminatingOrDeleted(piece))
                _gc.TryGCEntity(piece);
        }
    }

    private void CollectPlayers()
    {
        _players.Clear();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID == MapId.Nullspace || HasComp<GhostComponent>(uid))
                continue;

            _players.Add((xform.MapID, _transform.GetWorldPosition(xform)));
        }
    }

    private bool AnyPlayerWithin(MapId map, Vector2 pos, float range)
    {
        foreach (var (playerMap, playerPos) in _players)
        {
            if (playerMap == map && (playerPos - pos).LengthSquared() <= range * range)
                return true;
        }

        return false;
    }

    private bool TryPickPoint(AmbientSpaceFieldComponent field, NebulaDebrisComponent debris, MapId map, Vector2 center, out Vector2 point)
    {
        for (var i = 0; i < PointAttempts; i++)
        {
            point = center + _random.NextAngle().ToVec() * MathF.Sqrt(_random.NextFloat()) * field.Radius;
            if (!NebulaVeilHelpers.IsInMidZone(field, center, point) ||
                AnyPlayerWithin(map, point, debris.MinPlayerDistance))
                continue;

            _grids.Clear();
            _map.FindGridsIntersecting(map, Box2.CenteredAround(point, new Vector2(debris.Clearance * 2f)), ref _grids, approx: true, includeMap: false);
            if (_grids.Count == 0)
                return true;
        }

        point = default;
        return false;
    }

    private EntProtoId PickDebris(NebulaDebrisComponent debris)
    {
        var total = 0f;
        foreach (var weight in debris.Debris.Values)
        {
            total += weight;
        }

        var roll = _random.NextFloat() * total;
        EntProtoId last = default;
        foreach (var (proto, weight) in debris.Debris)
        {
            last = proto;
            roll -= weight;
            if (roll <= 0f)
                return proto;
        }

        return last;
    }
}
