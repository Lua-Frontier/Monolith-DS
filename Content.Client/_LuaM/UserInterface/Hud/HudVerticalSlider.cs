using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudVerticalSlider : Robust.Client.UserInterface.Controls.Range
{
    private const float TrackWidth = 8;
    private const float ThumbHeight = 6;
    private const float ThumbWidth = 20;

    private bool _grabbed;
    private bool _horizontal;

    public bool Horizontal
    {
        get => _horizontal;
        set
        {
            _horizontal = value;
            MinSize = value ? new Vector2(80, ThumbWidth + 4) : new Vector2(ThumbWidth + 4, 80);
        }
    }

    public HudVerticalSlider()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(ThumbWidth + 4, 80);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_horizontal)
        {
            DrawHorizontal(handle);
            return;
        }

        var scale = UIScale;
        var cx = MathF.Round(PixelSize.X / 2f);
        var thumbH = ThumbHeight * scale;
        var top = thumbH / 2f;
        var bottom = PixelSize.Y - thumbH / 2f;
        var halfTrack = TrackWidth * scale / 2f;

        var track = new UIBox2(cx - halfTrack, top, cx + halfTrack, bottom);
        handle.DrawRect(track, HudPalette.Background);

        var y = bottom - (bottom - top) * GetAsRatio();
        handle.DrawRect(new UIBox2(track.Left, y, track.Right, track.Bottom), HudPalette.BorderBright);
        StyleBoxHud.DrawFrame(handle, track, MathF.Max(1, MathF.Round(scale)), HudPalette.Border);

        var halfThumb = ThumbWidth * scale / 2f;
        var thumb = new UIBox2(cx - halfThumb, y - thumbH / 2f, cx + halfThumb, y + thumbH / 2f);
        handle.DrawRect(thumb, _grabbed ? HudPalette.Cream : HudPalette.Orange);
        StyleBoxHud.DrawFrame(handle, thumb, 1, Color.Black);
    }

    private void DrawHorizontal(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        var cy = MathF.Round(PixelSize.Y / 2f);
        var thumbW = ThumbHeight * scale;
        var left = thumbW / 2f;
        var right = PixelSize.X - thumbW / 2f;
        var halfTrack = TrackWidth * scale / 2f;

        var track = new UIBox2(left, cy - halfTrack, right, cy + halfTrack);
        handle.DrawRect(track, HudPalette.Background);

        var x = left + (right - left) * GetAsRatio();
        handle.DrawRect(new UIBox2(track.Left, track.Top, x, track.Bottom), HudPalette.BorderBright);
        StyleBoxHud.DrawFrame(handle, track, MathF.Max(1, MathF.Round(scale)), HudPalette.Border);

        var halfThumb = ThumbWidth * scale / 2f;
        var thumb = new UIBox2(x - thumbW / 2f, cy - halfThumb, x + thumbW / 2f, cy + halfThumb);
        handle.DrawRect(thumb, _grabbed ? HudPalette.Cream : HudPalette.Orange);
        StyleBoxHud.DrawFrame(handle, thumb, 1, Color.Black);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _grabbed = true;
        SetFromPosition(args.RelativePixelPosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick)
            _grabbed = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_grabbed)
            SetFromPosition(args.RelativePixelPosition);
    }

    private void SetFromPosition(Vector2 pixel)
    {
        var thumb = ThumbHeight * UIScale;
        var span = (_horizontal ? PixelSize.X : PixelSize.Y) - thumb;
        if (span <= 0)
            return;

        var t = ((_horizontal ? pixel.X : pixel.Y) - thumb / 2f) / span;
        SetAsRatio(Math.Clamp(_horizontal ? t : 1f - t, 0f, 1f));
    }
}
