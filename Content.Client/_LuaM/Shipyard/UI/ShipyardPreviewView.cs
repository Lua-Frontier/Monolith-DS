using System.Numerics;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._LuaM.Shipyard.UI;

/// <summary>
/// Shows the cached picture of a vessel with free panning and zoom; the picture is never re-rendered while viewed.
/// </summary>
public sealed partial class ShipyardPreviewView : Control
{
    [Dependency] private IEntityManager _entities = default!;

    private static readonly Color Background = Color.FromHex("#0B1118");
    private static readonly Color GridMinor = Color.FromHex("#131D27");
    private static readonly Color GridMajor = Color.FromHex("#1A2836");
    private static readonly Color LoadingShade = new(0f, 0f, 0f, 0.45f);
    private const float GridStep = 32f;
    private const int GridMajorEvery = 4;
    private const float ZoomStep = 1.25f;
    private const float FitMargin = 0.92f;
    private const float MaxScreenPixelsPerTile = 96f;

    private readonly ShipyardPreviewRenderSystem _render;
    private VesselPrototype? _vessel;
    private ShipyardPreviewEntry? _shown;
    private Vector2 _center;
    private float _zoom = 1f;
    private bool _fitted = true;
    private bool _dragging;

    /// <summary>
    /// Raised when the shown picture or the loading state changes.
    /// </summary>
    public event Action? StateChanged;

    public VesselPrototype? Vessel => _vessel;

    public ShipyardPreviewEntry? Entry => _vessel != null && _shown?.VesselId == _vessel.ID ? _shown : null;

    public bool Loading => _vessel != null && _render.IsLoading(_vessel.ID);

    public ShipyardPreviewView()
    {
        IoCManager.InjectDependencies(this);
        _render = _entities.System<ShipyardPreviewRenderSystem>();
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    public void SetVessel(VesselPrototype? vessel)
    {
        if (vessel?.ID == _vessel?.ID)
            return;

        _vessel = vessel;
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (_vessel == null)
        {
            _shown = null;
            StateChanged?.Invoke();
            return;
        }

        if (_render.Get(_vessel.ID) is { } entry)
        {
            _shown = entry;
            FitWhole();
        }
        else if (IsInsideTree)
        {
            _render.Request(_vessel);
        }

        StateChanged?.Invoke();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _render.PreviewChanged += OnPreviewChanged;
        ShowCurrent();
    }

    protected override void ExitedTree()
    {
        _render.PreviewChanged -= OnPreviewChanged;
        if (_vessel != null && _render.IsLoading(_vessel.ID))
            _render.Cancel();

        _dragging = false;
        base.ExitedTree();
    }

    private void OnPreviewChanged(string vesselId)
    {
        if (vesselId.Length == 0)
        {
            _shown = null;
            ShowCurrent();
            return;
        }

        if (_vessel?.ID != vesselId)
            return;

        _shown = _render.Get(vesselId);
        FitWhole();
        StateChanged?.Invoke();
    }

    private Vector2 TextureSize => _shown?.Texture?.Size ?? Vector2.Zero;

    private float MaxZoom => _shown is { PixelsPerTile: > 0 } shown
        ? MaxScreenPixelsPerTile / shown.PixelsPerTile
        : 1f;

    private float FitZoom()
    {
        var size = TextureSize;
        if (size.X <= 0 || size.Y <= 0 || PixelWidth <= 0 || PixelHeight <= 0)
            return 1f;

        return MathF.Min(PixelWidth / size.X, PixelHeight / size.Y) * FitMargin;
    }

    public void FitWhole()
    {
        _zoom = MathF.Min(FitZoom(), MaxZoom);
        if (_zoom > 1f)
            _zoom = MathF.Floor(_zoom);
        _center = TextureSize / 2f;
        _fitted = true;
    }

    public void ZoomIn()
    {
        ZoomAt((Vector2) PixelSize / 2f, true);
    }

    public void ZoomOut()
    {
        ZoomAt((Vector2) PixelSize / 2f, false);
    }

    /// <summary>
    /// Below 1:1 the zoom changes smoothly; above it only by whole steps, so every texel stays a square block of pixels.
    /// </summary>
    private static float StepZoom(float zoom, bool zoomIn)
    {
        if (zoomIn)
            return zoom >= 1f ? MathF.Floor(zoom + 0.001f) + 1f : MathF.Min(zoom * ZoomStep, 1f);

        return zoom > 1f ? MathF.Max(MathF.Ceiling(zoom - 0.001f) - 1f, 1f) : zoom / ZoomStep;
    }

    private void ZoomAt(Vector2 pixel, bool zoomIn)
    {
        if (_shown?.Texture == null)
            return;

        var half = (Vector2) PixelSize / 2f;
        var anchor = _center + (pixel - half) / _zoom;
        _zoom = Math.Clamp(StepZoom(_zoom, zoomIn), MathF.Min(FitZoom(), MaxZoom) * 0.5f, MaxZoom);
        _center = anchor - (pixel - half) / _zoom;
        _fitted = false;
        ClampCenter();
    }

    private void ClampCenter()
    {
        var size = TextureSize;
        _center = Vector2.Clamp(_center, Vector2.Zero, size);
    }

    protected override void Resized()
    {
        base.Resized();
        if (_fitted)
            FitWhole();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (args.Delta.Y != 0)
            ZoomAt(args.RelativePixelPosition, args.Delta.Y > 0);
        args.Handle();
    }

    private static bool IsDragKey(GUIBoundKeyEventArgs args)
    {
        return args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.UIRightClick;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (!IsDragKey(args))
            return;

        _dragging = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (!IsDragKey(args))
            return;

        _dragging = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging || _shown?.Texture == null)
            return;

        _center -= args.Relative * UIScale / _zoom;
        _fitted = false;
        ClampCenter();
        args.Handle();
    }

    protected override void Draw(IRenderHandle handle)
    {
        base.Draw(handle);

        _render.RenderPending(handle);

        var screen = handle.DrawingHandleScreen;
        var bounds = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        screen.DrawRect(bounds, Background);
        DrawGrid(screen);

        if (_shown?.Texture is not { } texture)
            return;

        var size = (Vector2) texture.Size * _zoom;
        var origin = (Vector2) PixelSize / 2f - _center * _zoom;
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));
        screen.DrawTextureRect(texture.Texture, UIBox2.FromDimensions(origin, size));

        if (_vessel != null && _shown.VesselId != _vessel.ID)
            screen.DrawRect(bounds, LoadingShade);
    }

    private void DrawGrid(DrawingHandleScreen screen)
    {
        var step = GridStep * UIScale;
        var columns = (int) (PixelWidth / step) + 1;
        var rows = (int) (PixelHeight / step) + 1;

        for (var i = 1; i < columns; i++)
        {
            var x = MathF.Round(i * step);
            screen.DrawLine(new Vector2(x, 0), new Vector2(x, PixelHeight), i % GridMajorEvery == 0 ? GridMajor : GridMinor);
        }

        for (var i = 1; i < rows; i++)
        {
            var y = MathF.Round(i * step);
            screen.DrawLine(new Vector2(0, y), new Vector2(PixelWidth, y), i % GridMajorEvery == 0 ? GridMajor : GridMinor);
        }
    }
}
