using Content.Shared._LuaM.Performance;
using Content.Shared.Explosion;
using Content.Shared.Tiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.Explosion.EntitySystems;

// -- Взрывы размером с пулю идут мимо очереди: без заливки тайлов и без визуал-сущности на всю карту, но со звуком и эффектом.
public sealed partial class ExplosionSystem
{
    private static readonly Vector2i[] MicroOffsets =
    {
        new(0, 0), new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private const int MicroMaxPerTick = 40;
    private static readonly TimeSpan MicroMaxTime = TimeSpan.FromMilliseconds(3);

    private readonly List<MicroExplosion> _microQueue = new();
    private readonly List<MicroExplosion> _microBatch = new();
    private readonly HashSet<EntityUid> _microProcessed = new();
    private readonly List<(Vector2i GridIndices, Tile Tile)> _microTiles = new();

    private readonly record struct MicroExplosion(
        EntityCoordinates Epicenter,
        ExplosionPrototype Proto,
        float TotalIntensity,
        float MaxTileIntensity,
        float TileBreakScale,
        int MaxTileBreak,
        bool CanCreateVacuum,
        EntityUid? Cause);

    private bool TryQueueMicroExplosion(EntityCoordinates epicenter,
        string typeId,
        float totalIntensity,
        float maxTileIntensity,
        float tileBreakScale,
        int maxTileBreak,
        bool canCreateVacuum,
        EntityUid? cause)
    {
        if (!_cfg.GetCVar(PerformanceCVars.MicroExplosions)
            || totalIntensity <= 0
            || totalIntensity > _cfg.GetCVar(PerformanceCVars.MicroExplosionMaxTotal)
            || maxTileIntensity > _cfg.GetCVar(PerformanceCVars.MicroExplosionMaxTile)
            || !HasComp<MapGridComponent>(epicenter.EntityId)
            || !_prototypeManager.TryIndex<ExplosionPrototype>(typeId, out var type))
            return false;

        _microQueue.Add(new MicroExplosion(epicenter, type, totalIntensity, maxTileIntensity, tileBreakScale,
            maxTileBreak, canCreateVacuum, cause));
        return true;
    }

    private void ProcessMicroExplosions()
    {
        if (_microQueue.Count == 0)
            return;

        _microBatch.AddRange(_microQueue);
        _microQueue.Clear();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var done = 0;
        try
        {
            while (done < _microBatch.Count && done < MicroMaxPerTick && watch.Elapsed < MicroMaxTime)
            {
                ApplyMicroExplosion(_microBatch[done]);
                done++;
            }
        }
        finally
        {
            if (done < _microBatch.Count)
                _microQueue.InsertRange(0, _microBatch.GetRange(done, _microBatch.Count - done));

            _microBatch.Clear();
        }
    }

    private void ApplyMicroExplosion(MicroExplosion micro)
    {
        var gridUid = micro.Epicenter.EntityId;
        if (TerminatingOrDeleted(gridUid)
            || !TryComp<MapGridComponent>(gridUid, out var grid)
            || !TryComp<BroadphaseComponent>(gridUid, out var lookup))
            return;

        if (TryComp<ProtectedGridComponent>(gridUid, out var protectedGrid) && protectedGrid.PreventExplosions)
            return;

        var mapPos = _transformSystem.ToMapCoordinates(micro.Epicenter);
        var center = _map.LocalToTile(gridUid, grid, micro.Epicenter);
        var centerExists = _map.TryGetTileRef(gridUid, grid, center, out var centerRef) && !centerRef.Tile.IsEmpty;
        var centerIntensity = centerExists ? MathF.Min(micro.MaxTileIntensity, micro.TotalIntensity) : 0f;
        var ringIntensity = MathF.Min(micro.MaxTileIntensity, (micro.TotalIntensity - centerIntensity) / 4f);
        var damageModifier = _damageableSystem.UniversalExplosionDamageModifier * _damageableSystem.UniversalAllDamageModifier;

        _microProcessed.Clear();
        _microTiles.Clear();
        for (var i = 0; i < MicroOffsets.Length; i++)
        {
            var intensity = i == 0 ? centerIntensity : ringIntensity;
            if (intensity <= 0f)
                continue;

            var tile = center + MicroOffsets[i];
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var damage = micro.Proto.DamagePerIntensity * (intensity * damageModifier);
            var throwForce = micro.Proto.ThrowEntitiesOnExplosion ? 10 * MathF.Sqrt(intensity) : 0f;
            var canDamageFloor = ExplodeTile(lookup,
                (gridUid, grid),
                tile,
                throwForce,
                damage,
                mapPos,
                _microProcessed,
                micro.Proto.ID,
                micro.Proto.FireStacks,
                micro.Proto.Temperature,
                intensity,
                micro.Cause);

            if (canDamageFloor)
                DamageFloorTile(tileRef, intensity * micro.TileBreakScale, micro.MaxTileBreak, micro.CanCreateVacuum, _microTiles, micro.Proto);
        }

        if (_microTiles.Count > 0 && !TerminatingOrDeleted(gridUid))
            _map.SetTiles(gridUid, grid, _microTiles);

        if (micro.Proto.FireColor == null)
            _explosionEffect.SpawnEffects(mapPos, 1);

        _audio.PlayStatic(micro.Proto.SmallSound, Filter.Pvs(mapPos), micro.Epicenter, true, micro.Proto.SmallSound.Params);
    }

    private float CombineDistance(QueuedExplosion queued, EntityCoordinates epicenter, float totalIntensity, float distance)
    {
        if (distance <= 0f
            || queued.Epicenter.EntityId != epicenter.EntityId
            || queued.TotalIntensity + totalIntensity > _cfg.GetCVar(PerformanceCVars.ExplosionMergeMaxTotal)
            || !HasComp<MapGridComponent>(epicenter.EntityId))
            return distance;

        return MathF.Max(distance, _cfg.GetCVar(PerformanceCVars.ExplosionMergeDistance));
    }
}
