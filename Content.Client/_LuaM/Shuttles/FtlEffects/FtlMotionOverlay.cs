/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 * Ported from Wolfgate (_WF/FtlEffects), AGPLv3.
 */

using System.Numerics;
using Content.Shared._LuaM.Shuttles.FtlEffects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Shuttles.FtlEffects;

// -- Перепроецирует отрисованный корпус на четверть секунды старта или прибытия, сущности не двигаются
public sealed partial class FtlMotionOverlay : Overlay
{
    private const int MaxBatchVertices = 8190;
    private static readonly ProtoId<ShaderPrototype> MotionShader = "LuaMFtlMotion";
    private static readonly ProtoId<ShaderPrototype> BackgroundShader = "LuaMFtlBackground";
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly FtlDepartureSystem _system;
    private readonly FtlMotionBackgroundOverlay _background;
    private readonly SharedTransformSystem _transforms;
    private readonly SharedMapSystem _maps;
    private readonly ShaderInstance _erase;
    private readonly Dictionary<EntityUid, Hull> _hulls = new();
    private readonly List<EntityUid> _expired = new();
    private readonly List<DrawVertexUV2D> _build = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public FtlMotionOverlay(FtlDepartureSystem system, FtlMotionBackgroundOverlay background)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _background = background;
        _transforms = _entities.System<SharedTransformSystem>();
        _maps = _entities.System<SharedMapSystem>();
        _erase = _prototypes.Index(BackgroundShader).Instance().Duplicate();
        ZIndex = 103;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _expired.Clear();
        foreach (var uid in _hulls.Keys)
        {
            if (_entities.Deleted(uid) || !_entities.HasComponent<FtlDepartureComponent>(uid))
                _expired.Add(uid);
        }
        foreach (var uid in _expired)
        {
            _hulls[uid].Shader.Dispose();
            _hulls.Remove(uid);
        }
        return _background.ViewportId == args.Viewport.Id && _background.Background != null &&
            _system.HasMotion(args.MapId, args.WorldAABB);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || _background.Background == null)
            return;
        var handle = args.WorldHandle;
        _erase.SetParameter("background", _background.Background);
        var query = _entities.EntityQueryEnumerator<FtlDepartureComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var effect, out var grid, out var xform))
        {
            var motion = _system.Motion(uid, effect);
            var gone = _system.Gone(uid, effect);
            if (xform.MapID != args.MapId || MathF.Abs(motion) < 0.0001f && !gone)
                continue;
            var matrix = _transforms.GetWorldMatrix(xform);
            var moved = _system.Moved(effect, grid, xform, matrix, motion);
            if (!args.WorldAABB.Intersects(matrix.TransformBox(grid.LocalAABB.Enlarged(0.5f))) &&
                !args.WorldAABB.Intersects(moved.TransformBox(grid.LocalAABB.Enlarged(0.5f))))
                continue;
            if (!_hulls.TryGetValue(uid, out var hull))
            {
                hull = new Hull(_prototypes.Index(MotionShader).Instance().Duplicate());
                _hulls.Add(uid, hull);
            }
            if (!hull.Built || hull.Tick != grid.LastTileModifiedTick)
                BuildHull(uid, grid, hull);

            // -- Закрашиваем фоном спрайты, вылезающие за края тайлов, не заливая промежутки между крыльями
            handle.UseShader(_erase);
            handle.SetTransform(matrix);
            DrawHull(handle, hull.Vertices);

            // -- Корабль уже ушёл, но сервер ещё не убрал его грид с этой карты
            if (gone)
                continue;

            var origin = args.Viewport.WorldToLocal(Vector2.Transform(Vector2.Zero, matrix));
            var right = args.Viewport.WorldToLocal(Vector2.Transform(Vector2.UnitX, matrix)) - origin;
            var forward = args.Viewport.WorldToLocal(Vector2.Transform(Vector2.UnitY, matrix)) - origin;
            origin.Y = args.Viewport.Size.Y - origin.Y;
            right.Y = -right.Y;
            forward.Y = -forward.Y;
            hull.Shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            hull.Shader.SetParameter("background", _background.Background);
            hull.Shader.SetParameter("originPixels", origin);
            hull.Shader.SetParameter("rightPixels", right);
            hull.Shader.SetParameter("forwardPixels", forward);
            handle.UseShader(hull.Shader);
            handle.SetTransform(moved);
            DrawHull(handle, hull.Vertices);
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void DrawHull(DrawingHandleWorld handle, DrawVertexUV2D[] vertices)
    {
        for (var offset = 0; offset < vertices.Length; offset += MaxBatchVertices)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White,
                new ReadOnlySpan<DrawVertexUV2D>(vertices, offset, Math.Min(MaxBatchVertices, vertices.Length - offset)));
    }

    private void BuildHull(EntityUid uid, MapGridComponent grid, Hull hull)
    {
        _build.Clear();
        var tiles = _maps.GetAllTiles(uid, grid);
        while (tiles.MoveNext(out var tile))
        {
            var origin = (Vector2) tile.Value.GridIndices * grid.TileSize;
            Add(origin, Vector2.Zero);
            Add(origin, Vector2.UnitX);
            Add(origin, Vector2.One);
            Add(origin, Vector2.Zero);
            Add(origin, Vector2.One);
            Add(origin, Vector2.UnitY);
        }
        hull.Vertices = _build.ToArray();
        hull.Tick = grid.LastTileModifiedTick;
        hull.Built = true;
        _build.Clear();

        void Add(Vector2 origin, Vector2 corner)
        {
            // -- Диагональные тайлы несут полноразмерные спрайты стен, поэтому квадрат чуть раздут
            var point = origin + corner * grid.TileSize + (corner - new Vector2(0.5f)) * 0.8f;
            _build.Add(new DrawVertexUV2D(point, point));
        }
    }

    protected override void DisposeBehavior()
    {
        _erase.Dispose();
        foreach (var hull in _hulls.Values)
            hull.Shader.Dispose();
        _hulls.Clear();
        base.DisposeBehavior();
    }

    private sealed class Hull(ShaderInstance shader)
    {
        public readonly ShaderInstance Shader = shader;
        public DrawVertexUV2D[] Vertices = Array.Empty<DrawVertexUV2D>();
        public GameTick Tick;
        public bool Built;
    }
}
