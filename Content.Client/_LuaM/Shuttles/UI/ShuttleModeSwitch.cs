using System.Numerics;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Drift / Dampen / Anchor as one black strip, the selected segment is lit behind its text.
/// </summary>
public sealed class ShuttleModeSwitch : Control
{
    private static readonly Color[] Lamps = { HudPalette.Caution, HudPalette.Good, HudPalette.Advisory };
    private static readonly TimeSpan ServerGrace = TimeSpan.FromSeconds(1);

    private readonly IGameTiming _timing;
    private readonly Label[] _labels = new Label[3];
    private readonly StyleBoxLamp[] _lamps = new StyleBoxLamp[3];
    private int _mode = 1;
    private bool _anchorLocked;
    private bool _hovered;
    private TimeSpan _pickedAt = TimeSpan.FromSeconds(-10);

    public event Action<int>? OnSelected;

    public bool AnchorLocked
    {
        get => _anchorLocked;
        set
        {
            if (_anchorLocked == value)
                return;
            _anchorLocked = value;
            UpdateLabels();
        }
    }

    public int Mode
    {
        get => _mode;
        set
        {
            value = Math.Clamp(value, 0, 2);
            // the server state lags behind a click, don't let it flip the switch back
            if (value == _mode || _timing.RealTime - _pickedAt < ServerGrace)
                return;
            _mode = value;
            UpdateLabels();
        }
    }

    public ShuttleModeSwitch()
    {
        _timing = IoCManager.Resolve<IGameTiming>();
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;
        MinHeight = 30;

        var font = LobbyHudStyles.UiFont(IoCManager.Resolve<IResourceCache>(), 13);
        for (var i = 0; i < 3; i++)
        {
            _lamps[i] = new StyleBoxLamp(Lamps[i], StyleBoxLamp.Lit.Fill);
            _labels[i] = new Label
            {
                Align = Label.AlignMode.Center,
                VAlign = Label.VAlignMode.Center,
                MouseFilter = MouseFilterMode.Ignore,
                FontOverride = font,
            };
            AddChild(_labels[i]);
        }

        UpdateLabels();
    }

    public void SetTexts(string drift, string dampen, string anchor)
    {
        _labels[0].Text = drift.ToUpperInvariant();
        _labels[1].Text = dampen.ToUpperInvariant();
        _labels[2].Text = anchor.ToUpperInvariant();
    }

    private void UpdateLabels()
    {
        for (var i = 0; i < 3; i++)
        {
            _labels[i].FontColorOverride = i == 2 && _anchorLocked
                ? HudPalette.Offline
                : i == _mode ? HudPalette.Void : HudPalette.TextDim;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var h = 0f;
        foreach (var label in _labels)
        {
            label.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            h = MathF.Max(h, label.DesiredSize.Y);
        }

        return new Vector2(0, h + 4);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var w = (finalSize.X - 4) / 3f;
        for (var i = 0; i < 3; i++)
            _labels[i].Arrange(UIBox2.FromDimensions(2 + w * i, 2, w, finalSize.Y - 4));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var px = MathF.Max(1, MathF.Round(UIScale));
        var box = new UIBox2(Vector2.Zero, PixelSize);
        handle.DrawRect(box, HudPalette.Background);
        StyleBoxHud.DrawFrame(handle, box, px, _hovered ? HudPalette.BorderBright : HudPalette.Border);

        var inner = new UIBox2(box.Left + 2 * px, box.Top + 2 * px, box.Right - 2 * px, box.Bottom - 2 * px);
        var w = inner.Width / 3f;
        for (var i = 1; i < 3; i++)
        {
            var x = MathF.Round(inner.Left + w * i);
            handle.DrawRect(new UIBox2(x, inner.Top + 4 * px, x + px, inner.Bottom - 4 * px), HudPalette.LineDim);
        }

        var lit = new UIBox2(MathF.Round(inner.Left + w * _mode), inner.Top, MathF.Round(inner.Left + w * (_mode + 1)), inner.Bottom);
        _lamps[_mode].Draw(handle, lit, UIScale);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        var px = MathF.Max(1, MathF.Round(UIScale));
        var w = (PixelSize.X - 4 * px) / 3f;
        if (w <= 0)
            return;

        var mode = Math.Clamp((int) ((args.RelativePixelPosition.X - 2 * px) / w), 0, 2);
        if (mode == _mode || mode == 2 && _anchorLocked)
            return;

        _mode = mode;
        _pickedAt = _timing.RealTime;
        UpdateLabels();
        OnSelected?.Invoke(mode);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }
}
