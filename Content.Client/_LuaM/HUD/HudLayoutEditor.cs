using System.Numerics;
using Content.Shared._LuaM.CCVar;
using Content.Shared._LuaM.Announcements;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Content.Shared._Crescent.Vessel;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Client.State;
using Content.Client._LuaM.Announcements;

namespace Content.Client._LuaM.Hud;

/// <summary>A read-only edit layer over the real native HUD.</summary>
public sealed partial class HudLayoutEditor : Popup
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _cache = default!;

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IStateManager _states = default!;

    private readonly AnnouncementPreview _preview;
    private readonly ScreenAnnouncementPreview _screenPreview;
    private NativeHudPreviewSession? _session;
    private bool _selectedScreen = true;
    private readonly OptionButton _elementSelection;
    private readonly Label _scaleLabel;
    private float _shownScale = -1;

    public HudLayoutEditor()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        CloseOnClick = false;
        var layout = new LayoutContainer();
        AddChild(layout);
        var (title, description) = GetPreviewText();
        _preview = new AnnouncementPreview(_cache, _cfg, title, description);
        layout.AddChild(_preview);
        LayoutContainer.SetAnchorPreset(_preview, LayoutContainer.LayoutPreset.Wide);
        _screenPreview = new ScreenAnnouncementPreview(_cfg);
        layout.AddChild(_screenPreview);
        LayoutContainer.SetAnchorPreset(_screenPreview, LayoutContainer.LayoutPreset.Wide);

        var toolbar = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.FromHex("#171E29F5")),
            Margin = new Thickness(8),
        };
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(10) };
        body.AddChild(new Label { Text = Loc.GetString("hud-layout-title") });
        body.AddChild(new Label { Text = Loc.GetString("hud-layout-instructions") });
        var selectionRow = new BoxContainer();
        selectionRow.AddChild(new Label { Text = Loc.GetString("hud-layout-selected") });
        _elementSelection = new OptionButton { Name = "HudLayoutSelectedElement", HorizontalExpand = true };
        _elementSelection.AddItem(Loc.GetString("hud-layout-element-location"), 0);
        _elementSelection.AddItem(Loc.GetString("hud-layout-element-announcement"), 1);
        _elementSelection.SelectId(1);
        selectionRow.AddChild(_elementSelection);
        body.AddChild(selectionRow);
        var enabled = new CheckBox
        {
            Name = "HudLayoutAnnouncementsEnabled",
            Text = Loc.GetString("hud-layout-announcements-enabled"),
            Pressed = _cfg.GetCVar(LuaMCVars.ScreenAnnouncementsEnabled),
        };
        body.AddChild(enabled);
        var locationAlignment = AddAlignment(body, "HudLayoutLocationAlignment", "hud-layout-location-alignment",
            _preview.Alignment, _preview.SetAlignment);
        var screenAlignment = AddAlignment(body, "HudLayoutScreenAlignment", "hud-layout-screen-alignment",
            _screenPreview.Alignment, _screenPreview.SetAlignment);
        var scaleRow = new BoxContainer();
        scaleRow.AddChild(new Label { Text = Loc.GetString("hud-layout-scale") });
        var smaller = new Button { Name = "HudLayoutScaleDown", Text = "−" };
        var larger = new Button { Name = "HudLayoutScaleUp", Text = "+" };
        _scaleLabel = new Label { Name = "HudLayoutScaleValue", MinWidth = 60, HorizontalAlignment = HAlignment.Center };
        scaleRow.AddChild(smaller);
        scaleRow.AddChild(_scaleLabel);
        scaleRow.AddChild(larger);
        var centerX = new Button { Name = "HudLayoutCenterX", Text = Loc.GetString("hud-layout-center-x") };
        var centerY = new Button { Name = "HudLayoutCenterY", Text = Loc.GetString("hud-layout-center-y") };
        scaleRow.AddChild(centerX);
        scaleRow.AddChild(centerY);
        body.AddChild(scaleRow);
        var resetElement = new Button { Name = "HudLayoutResetSelected", Text = Loc.GetString("hud-layout-reset-selected") };
        body.AddChild(resetElement);
        void SelectElement(bool screen)
        {
            _selectedScreen = screen;
            _elementSelection.SelectId(screen ? 1 : 0);
            locationAlignment.Parent!.Visible = !screen;
            screenAlignment.Parent!.Visible = screen;
            enabled.Visible = screen;
            _shownScale = -1;
        }
        _elementSelection.OnItemSelected += args => SelectElement(args.Id == 1);
        _preview.Selected += () => SelectElement(false);
        _screenPreview.Selected += () => SelectElement(true);
        smaller.OnPressed += _ => ChangeSelectedScale(-1);
        larger.OnPressed += _ => ChangeSelectedScale(1);
        centerX.OnPressed += _ => CenterSelected(true);
        centerY.OnPressed += _ => CenterSelected(false);
        resetElement.OnPressed += _ =>
        {
            if (_selectedScreen)
            {
                _screenPreview.Alignment = HudHorizontalAlignment.Center;
                _screenPreview.Anchor = HudLayout.DefaultScreenPosition;
                _screenPreview.Scale = 1;
                screenAlignment.SelectId((int) HudHorizontalAlignment.Center);
                enabled.Pressed = true;
            }
            else
            {
                _preview.Alignment = HudHorizontalAlignment.Center;
                _preview.Anchor = HudLayout.DefaultPosition;
                _preview.Scale = 1;
                locationAlignment.SelectId((int) HudHorizontalAlignment.Center);
            }
            _shownScale = -1;
        };
        SelectElement(true);
        var buttons = new BoxContainer();
        var save = new Button { Text = Loc.GetString("hud-layout-save") };
        var cancel = new Button { Text = Loc.GetString("hud-layout-cancel") };
        var reset = new Button { Text = Loc.GetString("hud-layout-reset") };
        save.OnPressed += _ =>
        {
            var anchor = HudLayout.Normalize(_preview.Anchor);
            _cfg.SetCVar(LuaMCVars.LocationAnnouncementX, anchor.X);
            _cfg.SetCVar(LuaMCVars.LocationAnnouncementY, anchor.Y);
            var screenAnchor = HudLayout.Normalize(_screenPreview.Anchor);
            _cfg.SetCVar(LuaMCVars.ScreenAnnouncementX, screenAnchor.X);
            _cfg.SetCVar(LuaMCVars.ScreenAnnouncementY, screenAnchor.Y);
            _cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, enabled.Pressed);
            _cfg.SetCVar(LuaMCVars.LocationAnnouncementAlignment, (int) _preview.Alignment);
            _cfg.SetCVar(LuaMCVars.ScreenAnnouncementAlignment, (int) _screenPreview.Alignment);
            _cfg.SetCVar(LuaMCVars.LocationAnnouncementScale, _preview.Scale);
            _cfg.SetCVar(LuaMCVars.ScreenAnnouncementScale, _screenPreview.Scale);
            _cfg.SaveToFile();
            Close();
        };
        cancel.OnPressed += _ => Close();
        reset.OnPressed += _ =>
        {
            _preview.Alignment = HudHorizontalAlignment.Center;
            _screenPreview.Alignment = HudHorizontalAlignment.Center;
            locationAlignment.SelectId((int) HudHorizontalAlignment.Center);
            screenAlignment.SelectId((int) HudHorizontalAlignment.Center);
            _preview.Anchor = HudLayout.DefaultPosition;
            _screenPreview.Anchor = HudLayout.DefaultScreenPosition;
            _preview.Scale = 1;
            _screenPreview.Scale = 1;
            enabled.Pressed = true;
            FocusPreview();
        };
        buttons.AddChild(save);
        buttons.AddChild(cancel);
        buttons.AddChild(reset);
        body.AddChild(buttons);
        toolbar.AddChild(body);
        layout.AddChild(toolbar);
        LayoutContainer.SetAnchorPreset(toolbar, LayoutContainer.LayoutPreset.CenterBottom);
        LayoutContainer.SetGrowHorizontal(toolbar, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(toolbar, LayoutContainer.GrowDirection.Begin);
        OnPopupHide += () =>
        {
            Orphan();
            _session?.Dispose();
            _session = null;
            UserInterfaceManager.GetUIController<ScreenAnnouncementUIController>().EditingLayout = false;
        };
    }

    private void ChangeSelectedScale(float delta)
    {
        if (_selectedScreen)
            _screenPreview.Scale = HudLayout.WheelScale(_screenPreview.Scale, delta, 1);
        else
            _preview.Scale = HudLayout.WheelScale(_preview.Scale, delta);
    }

    private UIBox2 SelectedBounds => _selectedScreen
        ? _screenPreview.ElementBounds.Translated(-(Vector2) GlobalPixelPosition)
        : _preview.ElementBounds;

    private void CenterSelected(bool horizontal)
    {
        var bounds = SelectedBounds;
        var point = bounds.TopLeft;
        if (horizontal) point.X = (PixelWidth - bounds.Width) / 2;
        else point.Y = (PixelHeight - bounds.Height) / 2;
        var preview = _selectedScreen ? (Control) _screenPreview : _preview;
        var origin = (Vector2) preview.GlobalPixelPosition - (Vector2) GlobalPixelPosition;
        var alignment = _selectedScreen ? _screenPreview.Alignment : _preview.Alignment;
        var converted = HudLayout.FromPosition(preview.PixelSize, bounds.Size, point - origin, alignment);
        // A second button can be clicked before the next layout pass. Preserve the other
        // anchor coordinate rather than reconstructing it from stale, pixel-rounded bounds.
        if (_selectedScreen)
            _screenPreview.Anchor = horizontal ? new Vector2(converted.X, _screenPreview.Anchor.Y)
                : new Vector2(_screenPreview.Anchor.X, converted.Y);
        else
            _preview.Anchor = horizontal ? new Vector2(converted.X, _preview.Anchor.Y)
                : new Vector2(_preview.Anchor.X, converted.Y);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (PixelWidth <= 0 || PixelHeight <= 0) return;
        var center = (Vector2) PixelSize / 2;
        // One spacing in both directions makes square cells. A widescreen display gets
        // approximately eight vertical guides and five horizontal guides on each side.
        var spacing = Math.Max(1, Math.Max(PixelWidth / 16f, PixelHeight / 10f));
        var thin = Color.FromHex("#83A7BF").WithAlpha(0.17f);
        for (var x = center.X % spacing; x < PixelWidth; x += spacing)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, PixelHeight), thin);
        for (var y = center.Y % spacing; y < PixelHeight; y += spacing)
            handle.DrawLine(new Vector2(0, y), new Vector2(PixelWidth, y), thin);
        void Cross(Vector2 point, Color color, float thickness)
        {
            point = Vector2.Clamp(point, Vector2.Zero, PixelSize);
            var half = thickness / 2;
            handle.DrawRect(new UIBox2(Math.Max(0, point.X - half), 0, Math.Min(PixelWidth, point.X + half), PixelHeight), color);
            handle.DrawRect(new UIBox2(0, Math.Max(0, point.Y - half), PixelWidth, Math.Min(PixelHeight, point.Y + half)), color);
        }
        Cross(center, Color.FromHex("#A6C8DF").WithAlpha(0.5f), Math.Max(2, 2 * UIScale));
        var bounds = SelectedBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        Cross(bounds.Center, Color.FromHex("#72C7FF").WithAlpha(0.45f), Math.Max(2, 2 * UIScale));
        handle.DrawRect(new UIBox2(bounds.TopLeft - new Vector2(5), bounds.BottomRight + new Vector2(5)), Color.FromHex("#72C7FF"), false);
    }

    private static OptionButton AddAlignment(BoxContainer body, string name, string label,
        HudHorizontalAlignment alignment, Action<HudHorizontalAlignment> changed)
    {
        var row = new BoxContainer();
        row.AddChild(new Label { Text = Loc.GetString(label) });
        var options = new OptionButton { Name = name };
        options.AddItem(Loc.GetString("hud-layout-align-left"), (int) HudHorizontalAlignment.Left);
        options.AddItem(Loc.GetString("hud-layout-align-center"), (int) HudHorizontalAlignment.Center);
        options.AddItem(Loc.GetString("hud-layout-align-right"), (int) HudHorizontalAlignment.Right);
        options.SelectId((int) alignment);
        options.OnItemSelected += args =>
        {
            options.SelectId(args.Id);
            changed((HudHorizontalAlignment) args.Id);
        };
        row.AddChild(options);
        body.AddChild(row);
        return options;
    }

    public void OpenFullScreen()
    {
        UserInterfaceManager.GetUIController<ScreenAnnouncementUIController>().EditingLayout = true;
        _session = new NativeHudPreviewSession(UserInterfaceManager, _cfg, _states, Close);
        UserInterfaceManager.ModalRoot.AddChild(this);
        Open(UIBox2.FromDimensions(Vector2.Zero, UserInterfaceManager.ModalRoot.Size));
        FocusPreview();
    }

    private void FocusPreview()
    {
        _preview.GrabKeyboardFocus();
    }

    private (string Title, string Description) GetPreviewText()
    {
        if (_entities.TryGetComponent<TransformComponent>(_player.LocalEntity, out var transform) &&
            transform.GridUid is { } grid && _entities.TryGetComponent<MetaDataComponent>(grid, out var metadata))
        {
            var description = _entities.TryGetComponent<VesselInfoComponent>(grid, out var vessel)
                ? vessel.Description : string.Empty;
            return (metadata.EntityName, description);
        }
        return (Loc.GetString("hud-layout-example-title"), Loc.GetString("hud-layout-example-description"));
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // A fullscreen popup follows its parent in both directions without reopening the modal.
        foreach (var child in Children)
            child.Measure(availableSize);
        return availableSize;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Visible)
        {
            var scale = _selectedScreen ? _screenPreview.Scale : _preview.Scale;
            if (_shownScale != scale)
            {
                _shownScale = scale;
                _scaleLabel.Text = $"{MathF.Round(scale * 100)}%";
            }
            var focused = _screenPreview.HasKeyboardFocus() || _preview.HasKeyboardFocus();
            if (!focused)
                FocusPreview();
        }
    }

    private sealed class AnnouncementPreview : Control
    {
        public event Action? Selected;
        public UIBox2 ElementBounds => _renderer.GetBounds(PixelSize, Anchor, _title, _description, Alignment, Scale);
        public float Scale = 1;
        public Vector2 Anchor;
        public HudHorizontalAlignment Alignment;

        public void SetAlignment(HudHorizontalAlignment alignment)
        {
            var bounds = _renderer.GetBounds(PixelSize, Anchor, _title, _description, Alignment, Scale);
            Alignment = HudLayout.NormalizeAlignment(alignment);
            Anchor = HudLayout.FromPosition(PixelSize, bounds.Size, bounds.TopLeft, Alignment);
        }
        private readonly LocationAnnouncementRenderer _renderer;
        private readonly string _title;
        private readonly string _description;
        private bool _dragging;
        private Vector2 _dragOffset;

        public AnnouncementPreview(IResourceCache cache, IConfigurationManager cfg, string title, string description)
        {
            Name = "AnnouncementPreview";
            _title = title;
            _description = description;
            _renderer = new LocationAnnouncementRenderer(cache);
            Scale = HudLayout.NormalizeScale(cfg.GetCVar(LuaMCVars.LocationAnnouncementScale));
            Anchor = HudLayout.Normalize(new Vector2(cfg.GetCVar(LuaMCVars.LocationAnnouncementX),
                cfg.GetCVar(LuaMCVars.LocationAnnouncementY)));
            Alignment = HudLayout.NormalizeAlignment((HudHorizontalAlignment) cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment));
            MouseFilter = MouseFilterMode.Stop;
            CanKeyboardFocus = true;
            KeyboardFocusOnClick = true;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var screen = (Vector2) PixelSize;
            var bounds = _renderer.GetBounds(screen, Anchor, _title, _description, Alignment, Scale);
            handle.DrawRect(new UIBox2(bounds.TopLeft - new Vector2(6), bounds.BottomRight + new Vector2(6)),
                Color.FromHex("#72C7FF"), false);
            _renderer.Draw(handle, screen, Anchor, _title, _description, _title.Length, _description.Length, Alignment, Scale);
        }

        protected override bool HasPoint(Vector2 point)
        {
            // This control fills the editor for normalized positioning, but only its visible
            // outline owns input. Other fullscreen HUD previews must remain independently draggable.
            var bounds = _renderer.GetBounds(PixelSize, Anchor, _title, _description, Alignment, Scale);
            return new UIBox2(bounds.TopLeft - new Vector2(6), bounds.BottomRight + new Vector2(6))
                .Contains(point * UIScale);
        }

        protected override void MouseWheel(GUIMouseWheelEventArgs args)
        {
            if (!HasPoint(args.RelativePosition)) return;
            Selected?.Invoke();
            Scale = HudLayout.WheelScale(Scale, args.Delta.Y);
            args.Handle();
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick)
            {
                // Several bindings can share the same mouse button. Let the UI click reach us;
                // HandleCanFocusDown already prevents these mouse actions from reaching gameplay.
                if (!args.CanFocus)
                    args.Handle();
                return;
            }
            var bounds = _renderer.GetBounds(PixelSize, Anchor, _title, _description, Alignment, Scale);
            var hit = new UIBox2(bounds.TopLeft - new Vector2(6), bounds.BottomRight + new Vector2(6));
            if (!hit.Contains(args.RelativePixelPosition))
                return;
            Selected?.Invoke();
            args.Handle();
            _dragging = true;
            _dragOffset = args.RelativePixelPosition - bounds.TopLeft;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            if (!_dragging)
                return;
            var bounds = _renderer.GetBounds(PixelSize, Anchor, _title, _description, Alignment, Scale);
            Anchor = HudLayout.FromPosition(PixelSize, bounds.Size, args.RelativePixelPosition - _dragOffset, Alignment);
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);
            args.Handle();
            if (args.Function == EngineKeyFunctions.UIClick)
                _dragging = false;
        }

        protected override void ControlFocusExited()
        {
            base.ControlFocusExited();
            _dragging = false;
        }

        protected override void KeyboardFocusExited()
        {
            base.KeyboardFocusExited();
            _dragging = false;
        }
    }
}
