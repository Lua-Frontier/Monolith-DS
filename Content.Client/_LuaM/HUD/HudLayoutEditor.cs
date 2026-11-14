using System.Numerics;
using Content.Shared._LuaM.CCVar;
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
    private NativeHudPreviewSession? _session;

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

        var toolbar = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.FromHex("#171E29F5")),
            Margin = new Thickness(8),
        };
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(10) };
        body.AddChild(new Label { Text = Loc.GetString("hud-layout-title") });
        body.AddChild(new Label { Text = Loc.GetString("hud-layout-instructions") });
        var buttons = new BoxContainer();
        var save = new Button { Text = Loc.GetString("hud-layout-save") };
        var cancel = new Button { Text = Loc.GetString("hud-layout-cancel") };
        var reset = new Button { Text = Loc.GetString("hud-layout-reset") };
        save.OnPressed += _ =>
        {
            var anchor = HudLayout.Normalize(_preview.Anchor);
            _cfg.SetCVar(LuaMCVars.LocationAnnouncementX, anchor.X);
            _cfg.SetCVar(LuaMCVars.LocationAnnouncementY, anchor.Y);
            _cfg.SaveToFile();
            Close();
        };
        cancel.OnPressed += _ => Close();
        reset.OnPressed += _ =>
        {
            _preview.Anchor = HudLayout.DefaultPosition;
            _preview.GrabKeyboardFocus();
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
        };
    }

    public void OpenFullScreen()
    {
        _session = new NativeHudPreviewSession(UserInterfaceManager, _cfg, _states, Close);
        UserInterfaceManager.ModalRoot.AddChild(this);
        Open(UIBox2.FromDimensions(Vector2.Zero, UserInterfaceManager.ModalRoot.Size));
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
        if (Visible && !_preview.HasKeyboardFocus())
            _preview.GrabKeyboardFocus();
    }

    private sealed class AnnouncementPreview : Control
    {
        public Vector2 Anchor;
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
            Anchor = HudLayout.Normalize(new Vector2(cfg.GetCVar(LuaMCVars.LocationAnnouncementX),
                cfg.GetCVar(LuaMCVars.LocationAnnouncementY)));
            MouseFilter = MouseFilterMode.Stop;
            CanKeyboardFocus = true;
            KeyboardFocusOnClick = true;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var screen = (Vector2) PixelSize;
            var bounds = _renderer.GetBounds(screen, Anchor, _title, _description);
            handle.DrawRect(new UIBox2(bounds.TopLeft - new Vector2(6), bounds.BottomRight + new Vector2(6)),
                Color.FromHex("#72C7FF"), false);
            _renderer.Draw(handle, screen, Anchor, _title, _description, _title.Length, _description.Length);
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
            var bounds = _renderer.GetBounds(PixelSize, Anchor, _title, _description);
            var hit = new UIBox2(bounds.TopLeft - new Vector2(6), bounds.BottomRight + new Vector2(6));
            if (!hit.Contains(args.RelativePixelPosition))
                return;
            args.Handle();
            _dragging = true;
            _dragOffset = args.RelativePixelPosition - bounds.TopLeft;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            if (!_dragging)
                return;
            var bounds = _renderer.GetBounds(PixelSize, Anchor, _title, _description);
            Anchor = HudLayout.FromPosition(PixelSize, bounds.Size, args.RelativePixelPosition - _dragOffset);
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
