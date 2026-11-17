using Content.Server.Atmos.Components;
using Content.Shared.Atmos;

namespace Content.Server.Atmos.EntitySystems;

// -- Комната, которую вент заново наполняет в дыру, иначе взрывно разгерметизируется с одного тайла бесконечно.
public sealed partial class AtmosphereSystem
{
    private static readonly TimeSpan BreachGap = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BreachLoopTime = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan BreachQuietTime = TimeSpan.FromMinutes(10);

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), BreachRun> _breachRuns = new();
    private TimeSpan _nextBreachPrune;

    private sealed class BreachRun
    {
        public TimeSpan Start;
        public TimeSpan Last;
        public TimeSpan QuietUntil;
        public bool Logged;
    }

    private int MonstermosSearchLimit(TransformComponent xform)
    {
        if (TryComp<MapAtmosphereComponent>(xform.MapUid, out var map) && !map.Space)
            return Atmospherics.MonstermosTileLimit;

        return Atmospherics.MonstermosHardTileLimit;
    }

    private bool BreachLooping(EntityUid grid, TileAtmosphere tile)
    {
        var now = _gameTiming.CurTime;
        PruneBreachRuns(now);

        var key = (grid, tile.GridIndices);
        if (!_breachRuns.TryGetValue(key, out var run))
        {
            _breachRuns[key] = new BreachRun { Start = now, Last = now };
            return false;
        }

        if (now < run.QuietUntil)
        {
            run.Last = now;
            return true;
        }

        if (now - run.Last > BreachGap)
            run.Start = now;
        run.Last = now;

        if (now - run.Start < BreachLoopTime)
            return false;

        run.Start = now;
        run.QuietUntil = now + BreachQuietTime;
        if (run.Logged)
            return true;

        run.Logged = true;
        Log.Info($"Explosive depressurization from {tile.GridIndices} on {ToPrettyString(grid)} has looped for {BreachLoopTime.TotalMinutes} minutes; quieting it.");
        return true;
    }

    private void MarkEqualized(int tileCount, int cycleNum)
    {
        for (var i = 0; i < tileCount; i++)
        {
            if (_equalizeTiles[i] is { } tile)
                tile.MonstermosInfo.LastCycle = cycleNum;
        }
    }

    private void PruneBreachRuns(TimeSpan now)
    {
        if (now < _nextBreachPrune)
            return;

        _nextBreachPrune = now + TimeSpan.FromMinutes(1);
        foreach (var (key, run) in _breachRuns)
        {
            if (now - run.Last > BreachGap && now >= run.QuietUntil)
                _breachRuns.Remove(key);
        }
    }
}
