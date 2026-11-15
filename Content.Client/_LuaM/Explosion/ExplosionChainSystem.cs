using System.Numerics;
using Content.Shared._Funkystation.Explosion.EntitySystems;
using Content.Shared._LuaM.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Explosion;

public sealed partial class ExplosionChainSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId ChainBlast = "ExplosionEffectLuaMChain";
    private static readonly EntProtoId ChainEmbers = "ExplosionEffectLuaMChainEmbers";

    private const int MinIteration = 2;
    private const int MaxBlasts = 16;
    private const float BlastsPerRadius = 1.2f;
    private const float DelayJitter = 0.08f;
    private static readonly TimeSpan IterationDelay = TimeSpan.FromSeconds(0.045);

    private readonly List<(TimeSpan Time, MapCoordinates Position)> _pending = new();
    private readonly List<(int Iteration, MapCoordinates Position)> _candidates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosionHideFireComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<ExplosionHideFireComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<ExplosionVisualsComponent>(ent, out var visuals))
            return;

        var radius = visuals.Intensity.Count;
        if (radius < SharedExplosionEffectSystem.MediumRadius)
            return;

        _candidates.Clear();

        foreach (var (gridUid, iterations) in visuals.Tiles)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            Collect(iterations, _transform.GetWorldMatrix(gridUid), grid.TileSize, Transform(gridUid).MapID);
        }

        if (visuals.SpaceTiles != null)
            Collect(visuals.SpaceTiles, visuals.SpaceMatrix, visuals.SpaceTileSize, visuals.Epicenter.MapId);

        if (_candidates.Count == 0)
            return;

        var count = Math.Min(MaxBlasts, (int) (radius * BlastsPerRadius));
        var now = _timing.CurTime;

        for (var i = 0; i < count; i++)
        {
            var (iteration, position) = _random.Pick(_candidates);
            var delay = IterationDelay * iteration + TimeSpan.FromSeconds(_random.NextFloat(0f, DelayJitter));
            _pending.Add((now + delay, position));
        }

        _candidates.Clear();
    }

    private void Collect(Dictionary<int, List<Vector2i>> iterations, Matrix3x2 matrix, ushort tileSize, MapId mapId)
    {
        foreach (var (iteration, tiles) in iterations)
        {
            if (iteration < MinIteration)
                continue;

            foreach (var tile in tiles)
            {
                var local = (tile + Vector2Helpers.Half) * tileSize;
                _candidates.Add((iteration, new MapCoordinates(Vector2.Transform(local, matrix), mapId)));
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var (time, position) = _pending[i];
            if (time > now)
                continue;

            _pending.RemoveAt(i);

            if (!_map.MapExists(position.MapId))
                continue;

            Spawn(ChainBlast, position);
            Spawn(ChainEmbers, position);
        }
    }
}
