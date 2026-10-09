// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Lua.Shipyard.UI;

/// <summary>
/// Radar of the shipyard console grid used to pick the docking port for purchased shuttles.
/// </summary>
public sealed class ShipyardDockRadarControl : BaseShuttleControl
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IMapManager _mapManager = default!;

    private readonly SharedTransformSystem _transform;

    private static readonly Color ShipyardTileColor = Color.FromHex("#1a1a1a");
    private static readonly Color ShipyardWallColor = Color.ToSrgb(Color.FromHex("#404040"));
    private static readonly Color HighlightDockColor = Color.Gold;
    private static readonly Color DockLabelColor = Color.White;
    private static readonly Color PlayerMarkerColor = Color.ToSrgb(Color.Cyan).WithAlpha(0.9f);

    private const float DockScale = 0.6f;
    private const float HighlightDockScale = 1.0f;
    private const float DragThresholdPx = 6f;
    private const float MinZoomRange = 3f;
    private const float PlayerMarkerRadius = 5f;

    protected override bool AllowResize => true;
    protected override bool ScaleWithControlSize => true;
    protected override Vector2 MidPointVector => new(PixelWidth / 2f, PixelHeight / 2f);

    private EntityCoordinates? _coordinates;
    private Dictionary<NetEntity, List<DockingPortState>> _docks = new();
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly HashSet<string> _labeledDocks = new();
    private bool _rangeInitialized;

    private bool _mouseDown;
    private bool _panning;
    private float _dragAccumulatedPx;

    /// <summary>
    /// Docking port drawn as selected.
    /// </summary>
    public NetEntity? HighlightDockPort { get; set; }

    /// <summary>
    /// Raised when the radar is left-clicked without dragging.
    /// </summary>
    public event Action<EntityCoordinates>? OnRadarClick;

    public ShipyardDockRadarControl() : base(MinZoomRange, 256f, 256f)
    {
        IoCManager.InjectDependencies(this);
        _transform = EntManager.System<SharedTransformSystem>();
    }

    public void UpdateState(NavInterfaceState state)
    {
        _coordinates = EntManager.GetCoordinates(state.Coordinates);
        _docks = state.Docks;

        WorldMaxRange = state.MaxRange;
        WorldMinRange = Math.Min(MinZoomRange, WorldMaxRange);

        if (!_rangeInitialized)
        {
            _rangeInitialized = true;
            ActualRadarRange = WorldMaxRange;
            WorldRange = WorldMaxRange;
        }

        ActualRadarRange = Math.Clamp(ActualRadarRange, WorldMinRange, WorldMaxRange);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        DrawBacking(handle);

        if (_coordinates is not { } coordinates
            || !EntManager.TryGetComponent<TransformComponent>(coordinates.EntityId, out var xform)
            || xform.MapID == MapId.Nullspace
            || MinimapScale <= 0f)
        {
            DrawNoSignal(handle);
            return;
        }

        var mapPos = _transform.ToMapCoordinates(coordinates).Position + Offset;
        var worldToView = Matrix3Helpers.CreateTranslation(-mapPos)
                          * Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale))
                          * Matrix3x2.CreateTranslation(MidPointVector);

        DrawCircles(handle);

        var viewExtents = new Vector2(PixelWidth, PixelHeight) / (2f * MinimapScale);
        _grids.Clear();
        _mapManager.FindGridsIntersecting(xform.MapID, new Box2(mapPos - viewExtents, mapPos + viewExtents), ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            var gridToView = _transform.GetWorldMatrix(grid.Owner) * worldToView;
            DrawGrid(handle, gridToView, grid, ShipyardTileColor, 0.9f, true);
        }

        foreach (var grid in _grids)
        {
            var gridToView = _transform.GetWorldMatrix(grid.Owner) * worldToView;
            DrawGrid(handle, gridToView, grid, ShipyardWallColor);
        }

        foreach (var (netGrid, docks) in _docks)
        {
            if (!EntManager.TryGetEntity(netGrid, out var gridUid))
                continue;

            var gridToView = _transform.GetWorldMatrix(gridUid.Value) * worldToView;
            DrawDocks(handle, docks, gridToView);
        }

        DrawLocalPlayerMarker(handle, xform.MapID, worldToView);
        DrawHint(handle);
    }

    private void DrawDocks(DrawingHandleScreen handle, List<DockingPortState> docks, Matrix3x2 gridToView)
    {
        var dockRadius = HighlightDockScale * MathF.Sqrt(2f) * MinimapScale;
        var viewBounds = new Box2(-dockRadius, -dockRadius, PixelWidth + dockRadius, PixelHeight + dockRadius);

        foreach (var state in docks)
        {
            var position = state.Coordinates.Position;
            if (!viewBounds.Contains(Vector2.Transform(position, gridToView)))
                continue;

            var highlighted = HighlightDockPort == state.Entity;
            var color = highlighted ? HighlightDockColor : Color.ToSrgb(state.HighlightedRadarColor);
            var scale = highlighted ? HighlightDockScale : DockScale;

            var verts = new[]
            {
                Vector2.Transform(position + new Vector2(-scale, -scale), gridToView),
                Vector2.Transform(position + new Vector2(scale, -scale), gridToView),
                Vector2.Transform(position + new Vector2(scale, scale), gridToView),
                Vector2.Transform(position + new Vector2(-scale, scale), gridToView),
                Vector2.Transform(position + new Vector2(-scale, -scale), gridToView),
            };

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts.AsSpan(0, 4), color.WithAlpha(0.8f));
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, verts, color);
        }

        // Labels go last so they are drawn on top of the docks.
        _labeledDocks.Clear();
        foreach (var state in docks)
        {
            if (state.LabelName == null || !_labeledDocks.Add(state.LabelName))
                continue;

            var uiPosition = Vector2.Transform(state.Coordinates.Position, gridToView);
            if (!viewBounds.Contains(uiPosition))
                continue;

            var labelDimensions = handle.GetDimensions(Font, state.LabelName, 0.9f);
            handle.DrawString(Font, (uiPosition / UIScale - labelDimensions / 2) * UIScale, state.LabelName, UIScale * 0.9f, DockLabelColor);
        }
    }

    private void DrawLocalPlayerMarker(DrawingHandleScreen handle, MapId mapId, Matrix3x2 worldToView)
    {
        if (_player.LocalEntity is not { } playerEnt
            || !EntManager.TryGetComponent<TransformComponent>(playerEnt, out var playerXform)
            || playerXform.MapID != mapId)
            return;

        var position = Vector2.Transform(_transform.GetWorldPosition(playerXform), worldToView);
        handle.DrawCircle(position, PlayerMarkerRadius * UIScale, PlayerMarkerColor);
        handle.DrawCircle(position, PlayerMarkerRadius * UIScale, Color.Black.WithAlpha(0.8f), filled: false);
    }

    private void DrawHint(DrawingHandleScreen handle)
    {
        var text = Loc.GetString("shipyard-console-dock-radar-hint");
        var padding = 6f * UIScale;
        var dimensions = handle.GetDimensions(Font, text, UIScale);
        handle.DrawString(Font, new Vector2(PixelWidth - dimensions.X - padding, padding), text, UIScale, Color.White.WithAlpha(0.9f));
    }

    /// <summary>
    /// Converts a position relative to the control into coordinates on the map.
    /// </summary>
    private EntityCoordinates GetMouseCoordinates(Vector2 relativePosition)
    {
        if (_coordinates is not { } coordinates || MinimapScale <= 0f)
            return EntityCoordinates.Invalid;

        var mapCoords = _transform.ToMapCoordinates(coordinates);
        var local = (relativePosition * UIScale - MidPointVector) / MinimapScale;
        var worldPos = mapCoords.Position + Offset + new Vector2(local.X, -local.Y);
        return _transform.ToCoordinates(new MapCoordinates(worldPos, mapCoords.MapId));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _mouseDown = true;
            _panning = false;
            _dragAccumulatedPx = 0f;
            args.Handle();
            return;
        }

        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (_mouseDown && !_panning)
            {
                var coords = GetMouseCoordinates(args.RelativePosition);
                if (coords.IsValid(EntManager))
                    OnRadarClick?.Invoke(coords);
            }

            _mouseDown = false;
            _panning = false;
            args.Handle();
            return;
        }

        base.KeyBindUp(args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_mouseDown)
            return;

        if (!_panning)
        {
            _dragAccumulatedPx += args.Relative.Length();
            if (_dragAccumulatedPx < DragThresholdPx)
                return;

            _panning = true;
        }

        if (MinimapScale <= 0f)
            return;

        Offset -= new Vector2(args.Relative.X, -args.Relative.Y) * UIScale / MinimapScale;
        Offset = Vector2.Clamp(Offset, -new Vector2(WorldMaxRange), new Vector2(WorldMaxRange));
        TargetOffset = Offset;
    }
}
