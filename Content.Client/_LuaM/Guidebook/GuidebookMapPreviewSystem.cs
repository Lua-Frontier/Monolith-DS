/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.IO;
using System.Linq;
using System.Numerics;
using System.Buffers.Binary;
using Content.Client.UserInterface.Controls;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Displays real MapRenderer PNGs. Never loads YAML, spawns entities or changes the player's eye.</summary>
public sealed partial class GuidebookMapPreviewSystem : EntitySystem
{
    [Dependency] private IClientNetManager _net = default!;
    private GuidebookMapPreviewWindow? _window;
    private GuidebookMapViewport? _view;
    private Texture? _texture;
    private Label? _status;
    private OptionButton? _parts;
    private string _path = "";
    private int _part, _request;
    private DateTime _retry;
    private byte[]? _receiving;
    private int _received;
    private string _receivingVersion = "";
    private readonly Dictionary<string, (string Version, byte[] Bytes)> _cache = new();
    public bool IsLoading { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GuidebookMapImageChunk>(Receive);
        SubscribeNetworkEvent<GuidebookImagesInvalidatedEvent>(_ =>
        {
            _cache.Clear();
            if (_window == null) return;
            _part = 0; _parts?.Clear();
            ClearTexture(); Request();
        });
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => { _cache.Clear(); Close(); });
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => { _cache.Clear(); Close(); });
        _net.Disconnect += Disconnected;
    }
    private void Disconnected(object? sender, NetDisconnectedArgs args) { _cache.Clear(); Close(); }
    public override void Shutdown() { _net.Disconnect -= Disconnected; _cache.Clear(); Close(); base.Shutdown(); }

    public void Open(string mapPath, string displayName)
    {
        if (!GuidebookMapRendering.Enabled) return;
        Close(); _path = mapPath; _part = 0;
        var window = new GuidebookMapPreviewWindow { Title = displayName, MinSize = new(640, 480), SetSize = new(1000, 760) };
        _window = window;
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(10) };
        window.ContentsContainer.AddChild(body);
        _status = new Label { Text = "Проверка актуальности плана…" }; body.AddChild(_status);
        _parts = new OptionButton { HorizontalExpand = true, Visible = false };
        _parts.OnItemSelected += e => { _parts.SelectId(e.Id); _part = e.Id; ClearTexture(); Request(); };
        body.AddChild(_parts);
        var view = new GuidebookMapViewport { HorizontalExpand = true, VerticalExpand = true }; _view = view; body.AddChild(view);
        var controls = new BoxContainer(); body.AddChild(controls);
        foreach (var (text, action) in new (string, Action)[]
        {
            ("+", () => view.ChangeZoom(1 / 1.25f)), ("−", () => view.ChangeZoom(1.25f)), ("Вписать целиком", view.Fit),
            ("←", () => view.Pan(new(-1, 0))), ("→", () => view.Pan(new(1, 0))),
            ("↑", () => view.Pan(new(0, -1))), ("↓", () => view.Pan(new(0, 1))),
        }) { var button = new Button { Text = text }; button.OnPressed += _ => action(); controls.AddChild(button); }
        body.AddChild(new Label { Text = "Колесо — масштаб · ЛКМ/ПКМ + движение мыши — сдвиг" });
        window.OnClose += Close; window.Removed += Close;
        window.OpenCentered(); view.GrabKeyboardFocus(); Request();
    }
    private string CacheKey => _path + "#" + _part;
    private void Request()
    {
        if (_window == null) return;
        _request++; _receiving = null; _received = 0; IsLoading = true;
        if (_status != null) { _status.Visible = true; _status.Text = _net.IsConnected ? "Проверка актуальности плана…" : "План доступен после подключения к серверу."; }
        _retry = DateTime.UtcNow.AddSeconds(3);
        if (_net.IsConnected) RaiseNetworkEvent(new RequestGuidebookMapImage(_path, _request, _cache.GetValueOrDefault(CacheKey).Version ?? "", _part));
    }
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (IsLoading && _window != null && DateTime.UtcNow >= _retry) Request();
    }
    private void Receive(GuidebookMapImageChunk chunk)
    {
        if (_window == null || chunk.Request != _request) return;
        if (chunk.Status.Length > 0)
        {
            ClearTexture(); _receiving = null;
            IsLoading = chunk.Status == "pending"; _retry = DateTime.UtcNow.AddSeconds(2);
            if (_status != null) { _status.Visible = true; _status.Text = IsLoading ? "План обновляется. Ожидание актуального изображения…" : "Актуальный план пока недоступен."; }
            return;
        }
        if (_parts != null && _parts.ItemCount != chunk.Parts && chunk.Parts is > 0 and <= 100)
        {
            _parts.Clear(); for (var i = 0; i < chunk.Parts; i++) _parts.AddItem("Часть карты " + (i + 1), i);
            _parts.SelectId(_part); _parts.Visible = chunk.Parts > 1;
        }
        if (chunk.Total == 0)
        {
            if (_cache.TryGetValue(CacheKey, out var cached) && cached.Version == chunk.Version) Show(cached.Bytes);
            return;
        }
        if (chunk.Total is <= 0 or > 48 * 1024 * 1024 || chunk.Data.Length > 32 * 1024 || chunk.Offset < 0 || chunk.Offset > chunk.Total - chunk.Data.Length) return;
        if (chunk.Offset == 0) { _receiving = new byte[chunk.Total]; _received = 0; _receivingVersion = chunk.Version; }
        if (_receiving == null || _receiving.Length != chunk.Total || _received != chunk.Offset || _receivingVersion != chunk.Version) return;
        Array.Copy(chunk.Data, 0, _receiving, chunk.Offset, chunk.Data.Length); _received += chunk.Data.Length;
        _retry = DateTime.UtcNow.AddSeconds(20);
        if (_status != null) _status.Text = $"Загрузка плана: {_received * 100L / chunk.Total}%";
        if (_received != chunk.Total) return;
        var bytes = _receiving; _receiving = null;
        if (_cache.Count >= 2 && !_cache.ContainsKey(CacheKey)) _cache.Remove(_cache.Keys.First());
        _cache[CacheKey] = (chunk.Version, bytes); Show(bytes);
    }
    private void Show(byte[] bytes)
    {
        IsLoading = false;
        try
        {
            // Bound decoded memory as well as compressed/network size.
            if (bytes.Length < 24 || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4)) != 0x89504E47) throw new InvalidDataException();
            var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            if (width is <= 0 or > 8192 || height is <= 0 or > 8192) throw new InvalidDataException("Image dimensions exceed the guidebook limit.");
            ClearTexture(); using var stream = new MemoryStream(bytes, false);
            _texture = Texture.LoadFromPNGStream(stream, "Guidebook MapRender"); _view?.SetImage(_texture);
            if (_status != null) _status.Visible = false;
        }
        catch (Exception e) { Log.Warning("Invalid guidebook MapRender: " + e.Message); if (_status != null) _status.Text = "Не удалось прочитать MapRender."; _cache.Remove(CacheKey); }
    }
    private void ClearTexture() { _view?.SetImage(null); (_texture as IDisposable)?.Dispose(); _texture = null; }
    public void Close()
    {
        _request++; IsLoading = false; _receiving = null;
        var window = _window; _window = null;
        ClearTexture(); _view = null; _status = null; _parts = null;
        if (window == null) return;
        window.OnClose -= Close; window.Removed -= Close;
        if (window.Parent != null) window.Close();
    }
}

internal sealed class GuidebookMapPreviewWindow : FancyWindow
{
    public event Action? Removed;
    protected override void ExitedTree() { base.ExitedTree(); Removed?.Invoke(); }
}

internal sealed class GuidebookMapViewport : Control
{
    private Texture? _image;
    private Vector2 _center;
    private float _zoom = 1;
    private bool _dragging;
    public GuidebookMapViewport() { MouseFilter = MouseFilterMode.Stop; CanKeyboardFocus = true; KeyboardFocusOnClick = true; RectClipContent = true; MinHeight = 280; }
    public void SetImage(Texture? image) { _image = image; Fit(); }
    public void Fit()
    {
        if (_image == null) return;
        _center = new Vector2(_image.Width, _image.Height) / 2;
        _zoom = Math.Max(_image.Width / (float) Math.Max(1, PixelWidth), _image.Height / (float) Math.Max(1, PixelHeight)) * 1.05f;
    }
    public void ChangeZoom(float factor) => _zoom = Math.Clamp(_zoom * factor, .05f, 100);
    public void Pan(Vector2 direction) => _center += direction * _zoom * 40;
    protected override void Resized() { base.Resized(); Fit(); }
    protected override void MouseWheel(GUIMouseWheelEventArgs args) { ChangeZoom(MathF.Pow(1.2f, -args.Delta.Y)); args.Handle(); }
    private static bool DragKey(GUIBoundKeyEventArgs args) => args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.Use || args.Function == EngineKeyFunctions.UseSecondary;
    protected override void KeyBindDown(GUIBoundKeyEventArgs args) { if (!DragKey(args)) return; _dragging = true; args.Handle(); }
    protected override void KeyBindUp(GUIBoundKeyEventArgs args) { if (!DragKey(args)) return; _dragging = false; args.Handle(); }
    protected override void MouseMove(GUIMouseMoveEventArgs args) { base.MouseMove(args); if (!_dragging) return; _center -= args.Relative * UIScale * _zoom; args.Handle(); }
    protected override void Draw(IRenderHandle handle)
    {
        var screen = handle.DrawingHandleScreen;
        screen.DrawRect(UIBox2.FromDimensions(Vector2.Zero, PixelSize), Color.Black);
        if (_image == null) return;
        var size = new Vector2(_image.Width, _image.Height) / _zoom;
        var position = (Vector2) PixelSize / 2 - _center / _zoom;
        screen.DrawTextureRect(_image, UIBox2.FromDimensions(position, size));
    }
}
