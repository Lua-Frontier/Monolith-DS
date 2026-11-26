using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.UserInterface.Hud;

/// <summary>
/// Long button texts start at the left and are cut at the end; while the button is hovered the text runs left in a loop.
/// </summary>
public sealed class HudMarquee : Control
{
    private const float Speed = 40;
    private const float Gap = 36;

    private readonly Button _button;
    private readonly Label _tail;
    private Thickness _rest;
    private float _cycle;
    private float _offset;
    private bool _running;

    private HudMarquee(Button button)
    {
        _button = button;
        MouseFilter = MouseFilterMode.Ignore;
        button.RectClipContent = true;
        button.ClipText = true;
        button.Label.RemoveStyleClass(Button.StyleClassButton);
        button.Label.Align = Label.AlignMode.Left;

        _tail = new Label { ClipText = true, Align = Label.AlignMode.Left, Visible = false };
        button.AddChild(_tail);
    }

    public static void Attach(Button button)
    {
        button.AddChild(new HudMarquee(button));
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var label = _button.Label;
        if (!_running)
        {
            if (!_button.IsHovered || !Start(label))
                return;
        }
        else if (!_button.IsHovered)
        {
            Stop(label);
            return;
        }

        if (!ReferenceEquals(_tail.Text, label.Text))
            _tail.Text = label.Text;
        if (_tail.FontOverride != label.FontOverride)
            _tail.FontOverride = label.FontOverride;

        _offset = (_offset + Speed * args.DeltaSeconds) % _cycle;
        var shift = MathF.Round(_offset);
        label.Margin = new Thickness(_rest.Left - shift, _rest.Top, _rest.Right - _cycle, _rest.Bottom);
        _tail.Margin = new Thickness(_rest.Left - shift + _cycle, _rest.Top, _rest.Right - 2 * _cycle, _rest.Bottom);
    }

    private bool Start(Label label)
    {
        if (string.IsNullOrEmpty(label.Text) || label.Size.X <= 0)
            return false;

        _rest = label.Margin;
        label.ClipText = false;
        label.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        var width = label.DesiredSize.X - _rest.Left - _rest.Right;
        label.ClipText = true;
        label.InvalidateMeasure();
        if (width <= label.Size.X + 0.5f)
            return false;

        _cycle = width + Gap;
        _offset = 0;
        _running = true;
        _tail.Visible = true;
        return true;
    }

    private void Stop(Label label)
    {
        _running = false;
        _tail.Visible = false;
        label.Margin = _rest;
    }
}
