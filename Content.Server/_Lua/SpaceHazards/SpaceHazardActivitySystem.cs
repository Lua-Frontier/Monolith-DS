// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.SpaceHazards;

/// <summary>
/// Keeps the list of space hazards that have a player nearby, so the rest of them cost nothing.
/// </summary>
public sealed partial class SpaceHazardActivitySystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private const float CellSize = 512f;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextCheck;
    private float _maxActivationRange = 1024f;
    private readonly Dictionary<(MapId Map, int X, int Y), List<EntityUid>> _cells = new();
    private readonly Dictionary<EntityUid, HazardIndex> _index = new();
    private readonly HashSet<EntityUid> _active = new();
    private readonly HashSet<EntityUid> _seenThisPass = new();
    private readonly List<EntityUid> _activeScratch = new();
    private readonly Dictionary<MapId, List<Vector2>> _playersByMap = new();

    public IReadOnlyCollection<EntityUid> ActiveHazards => _active;

    private readonly record struct HazardIndex(MapId Map, Vector2 Pos, float Range, int CellX, int CellY);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceHazardActivityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpaceHazardActivityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, SpaceHazardActivityComponent activity, MapInitEvent args)
    {
        Register(uid, activity);
    }

    private void OnShutdown(EntityUid uid, SpaceHazardActivityComponent activity, ComponentShutdown args)
    {
        Unregister(uid);
    }

    /// <summary>
    /// Sets how close to the hazard's center a player has to be for it to wake up.
    /// </summary>
    public void SetActivationRange(Entity<SpaceHazardActivityComponent?> ent, float range)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.ActivationRange = range;
        Register(ent, ent.Comp);
    }

    // Hazards don't move, so their cell is fixed from here on.
    private void Register(EntityUid uid, SpaceHazardActivityComponent activity)
    {
        if (_index.ContainsKey(uid))
            Unregister(uid);

        if (!TryComp(uid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
            return;

        var pos = _transform.GetWorldPosition(xform);
        var range = MathF.Max(activity.ActivationRange, 1f);
        _maxActivationRange = MathF.Max(_maxActivationRange, range);
        var cellX = CellCoord(pos.X);
        var cellY = CellCoord(pos.Y);
        var key = (xform.MapID, cellX, cellY);
        if (!_cells.TryGetValue(key, out var list))
        {
            list = new List<EntityUid>(8);
            _cells[key] = list;
        }

        list.Add(uid);
        _index[uid] = new HazardIndex(xform.MapID, pos, range, cellX, cellY);
        if (activity.Active)
            _active.Add(uid);
    }

    private void Unregister(EntityUid uid)
    {
        _active.Remove(uid);
        if (!_index.Remove(uid, out var info))
            return;

        var key = (info.Map, info.CellX, info.CellY);
        if (!_cells.TryGetValue(key, out var list))
            return;

        list.Remove(uid);
        if (list.Count == 0)
            _cells.Remove(key);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        _seenThisPass.Clear();
        BuildPlayerPositions();
        var cellRadius = Math.Max(1, (int) MathF.Ceiling(_maxActivationRange / CellSize));

        foreach (var (mapId, players) in _playersByMap)
        {
            foreach (var playerPos in players)
            {
                var cx = CellCoord(playerPos.X);
                var cy = CellCoord(playerPos.Y);
                for (var dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    for (var dy = -cellRadius; dy <= cellRadius; dy++)
                    {
                        if (!_cells.TryGetValue((mapId, cx + dx, cy + dy), out var hazards))
                            continue;

                        foreach (var uid in hazards)
                        {
                            if (!_index.TryGetValue(uid, out var info) ||
                                !TryComp(uid, out SpaceHazardActivityComponent? activity))
                                continue;

                            if ((playerPos - info.Pos).LengthSquared() > info.Range * info.Range)
                                continue;

                            _seenThisPass.Add(uid);
                            activity.LastSeenPlayer = now;
                            if (activity.Active)
                                continue;

                            activity.Active = true;
                            _active.Add(uid);
                        }
                    }
                }
            }
        }

        _activeScratch.Clear();
        _activeScratch.AddRange(_active);
        foreach (var uid in _activeScratch)
        {
            if (_seenThisPass.Contains(uid))
                continue;

            if (!TryComp(uid, out SpaceHazardActivityComponent? activity) || !activity.Active)
            {
                _active.Remove(uid);
                continue;
            }

            if (activity.LastSeenPlayer is not { } last)
            {
                activity.LastSeenPlayer = now;
                continue;
            }

            if (now - last < activity.IdleTimeout)
                continue;

            activity.Active = false;
            _active.Remove(uid);
        }
    }

    private void BuildPlayerPositions()
    {
        foreach (var list in _playersByMap.Values)
        {
            list.Clear();
        }

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;

            var pos = xform.GridUid is { } grid ? _transform.GetWorldPosition(grid) : _transform.GetWorldPosition(xform);
            if (!_playersByMap.TryGetValue(xform.MapID, out var list))
            {
                list = new List<Vector2>();
                _playersByMap[xform.MapID] = list;
            }

            list.Add(pos);
        }
    }

    private static int CellCoord(float value) => (int) MathF.Floor(value / CellSize);
}
