using System.Numerics;
using Content.Client._LuaM.Hud;
using Content.Shared._LuaM.Announcements;
using Content.Shared._LuaM.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Announcements;

/// <summary>Drags the same native card used for received announcements, in the same UI coordinates.</summary>
public sealed class ScreenAnnouncementPreview : LayoutContainer
{
    public event Action? Selected;
    public UIBox2 ElementBounds => UIBox2.FromDimensions(_card.GlobalPixelPosition, _card.PixelSize);
    public float Scale = 1;
    public Vector2 Anchor;
    public HudHorizontalAlignment Alignment;

    public void SetAlignment(HudHorizontalAlignment alignment)
    {
        var position = HudLayout.Position(Size, _card.Size, Anchor, Alignment, allowHorizontalOverflow: true);
        Alignment = HudLayout.NormalizeAlignment(alignment);
        Anchor = HudLayout.FromPosition(Size, _card.Size, position, Alignment, allowHorizontalOverflow: true);
    }
    private readonly ScreenAnnouncementCard _card = new();
    private bool _dragging;
    private Vector2 _offset;

    public ScreenAnnouncementPreview(IConfigurationManager cfg)
    {
        Name = "ScreenAnnouncementPreview";
        _card.PreviewMode = true;
        Scale = HudLayout.NormalizeScale(cfg.GetCVar(LuaMCVars.ScreenAnnouncementScale), 1);
        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        Anchor = HudLayout.Normalize(new Vector2(cfg.GetCVar(LuaMCVars.ScreenAnnouncementX), cfg.GetCVar(LuaMCVars.ScreenAnnouncementY)));
        Alignment = HudLayout.NormalizeAlignment((HudHorizontalAlignment) cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment));
        AddChild(_card);
        _card.SetAnnouncement(new ScreenAnnouncementEvent(
            Loc.GetString("screen-announcement-preview-title-system"),
            Loc.GetString("screen-announcement-preview-body-system"), ScreenAnnouncementStyle.System));
        _card.SetProgress(1);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _card.MaxWidth = float.PositiveInfinity;
        _card.MaxHeight = Math.Max(1, Size.Y - 24);
        _card.PresentationScale = Scale;
        SetPosition(_card, HudLayout.Position(Size, _card.Size, Anchor, Alignment, allowHorizontalOverflow: true));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var margin = new Vector2(5 * UIScale);
        handle.DrawRect(new UIBox2(_card.PixelPosition - margin, _card.PixelPosition + _card.PixelSize + margin),
            Color.FromHex("#72C7FF"), false);
    }

    protected override bool HasPoint(Vector2 point)
    {
        // Fullscreen for positioning, but transparent to hit-testing outside the card's outline.
        return new UIBox2(_card.Position - new Vector2(5), _card.Position + _card.Size + new Vector2(5)).Contains(point);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        if (!HasPoint(args.RelativePosition)) return;
        Selected?.Invoke();
        Scale = HudLayout.WheelScale(Scale, args.Delta.Y, 1);
        _card.PresentationScale = Scale;
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
        {
            if (!args.CanFocus)
                args.Handle();
            return;
        }
        if (!HasPoint(args.RelativePosition))
            return;
        args.Handle();
        Selected?.Invoke();
        _dragging = true;
        _offset = args.RelativePosition - _card.Position;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
            Anchor = HudLayout.FromPosition(Size, _card.Size, args.RelativePosition - _offset, Alignment, allowHorizontalOverflow: true);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _dragging = false;
        args.Handle();
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
