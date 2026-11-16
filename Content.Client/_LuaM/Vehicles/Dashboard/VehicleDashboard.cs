using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._LuaM.Vehicles.Dashboard;

public sealed class VehicleDashboard : VehicleHudPanel
{
    private const float TachScaleMax = 7f;
    private const float LimitRowTop = 22f;
    private const float LimitRowHeight = 18f;
    private const float BodyTop = LimitRowTop + LimitRowHeight + 4f;
    private const float TrackLeft = 108f;
    private const float TrackRight = 312f;
    private const float TrackMiddle = LimitRowTop + LimitRowHeight / 2f;

    public float Tach;
    public float Thrust;
    public float Fuel;
    public float Speed;
    public float MaxSpeed = 60f;
    public float SpeedLimit = 1f;
    public float MinSpeedLimit = 0.1f;
    public float Hull = 1f;
    public bool EngineOn;
    public bool EngineStarting;
    public bool Lights;
    public bool Handbrake;
    public bool Boost;
    public bool HasFuel = true;
    public bool Hover;
    public string Gear = "N";

    private static readonly string TachFormat = "{0}" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "{1}";

    private bool _draggingLimit;
    private string? _limitFormat;

    private string LimitFormat => _limitFormat ??= "{0} " + Locale("luam-vehicle-dashboard-kmh");

    public bool DraggingLimit => _draggingLimit;

    public event Action<float>? SpeedLimitChanged;

    public VehicleDashboard() : base(380f, 132f)
    {
    }

    protected override float DrawHeader(DrawingHandleScreen handle, float right, float s)
    {
        var (status, color) = !HasFuel
            ? (Locale("luam-vehicle-dashboard-no-fuel"), Danger)
            : EngineStarting
                ? (Locale("luam-vehicle-dashboard-starting"), Warn)
                : EngineOn
                    ? (Locale("luam-vehicle-dashboard-running"), Good)
                    : (Locale("luam-vehicle-dashboard-off"), Muted);

        var x = StatusLight(handle, right, status, color, s) - 6f;
        x = HeaderIndicator(handle, x, Locale("luam-vehicle-dashboard-boost"), Boost ? Warn : null, s);
        x = HeaderIndicator(handle, x, Locale("luam-vehicle-dashboard-handbrake"), Handbrake ? Danger : null, s);
        x = HeaderIndicator(handle, x, Locale("luam-vehicle-dashboard-lights"), Lights ? Accent : null, s);
        return x;
    }

    private float HeaderIndicator(DrawingHandleScreen handle, float right, string text, Color? color, float s)
    {
        var width = IndicatorWidth(text, s);
        Indicator(handle, right - width, 3, width, 12, text, color, s);
        return right - width - 4f;
    }

    protected override void DrawBody(DrawingHandleScreen handle, float s)
    {
        DrawLimitRow(handle, s);
        DrawSpeedBlock(handle, s);
        if (Hover)
            DrawThrustBlock(handle, s);
        else
            DrawTachBlock(handle, s);

        DrawGaugesBlock(handle, s);
    }

    private void DrawLimitRow(DrawingHandleScreen handle, float s)
    {
        Box(handle, 6, LimitRowTop, 368, LimitRowHeight, Inset, SectionBorder, s);
        var limited = SpeedLimit < 0.999f;
        DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-dashboard-limiter"), TrackLeft - 18f, s), 12, TrackMiddle, s, limited ? Warn : Muted);

        handle.DrawRect(Rect(TrackLeft, TrackMiddle - 1.5f, TrackRight - TrackLeft, 3, s), Off);
        var knob = TrackLeft + (TrackRight - TrackLeft) * LimitToFraction(SpeedLimit);
        handle.DrawRect(Rect(TrackLeft, TrackMiddle - 1.5f, knob - TrackLeft, 3, s), limited ? Warn : Accent);
        Box(handle, knob - 3f, TrackMiddle - 6f, 6, 12, _draggingLimit ? Accent : LitFill, limited ? Warn : Accent, s);

        var value = limited
            ? Format(LimitFormat, (int) MathF.Round(MaxSpeed * SpeedLimit))
            : Locale("luam-vehicle-dashboard-limiter-off");
        DrawMiddleRight(handle, Label, value, 368, TrackMiddle, s, limited ? Warn : Muted);
    }

    private void DrawSpeedBlock(DrawingHandleScreen handle, float s)
    {
        const float t = BodyTop - 24f;
        Box(handle, 6, 24 + t, 132, 82, Inset, SectionBorder, s);
        DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-dashboard-speed"), 120, s), 12, 31 + t, s, Muted);

        var digits = LeadingZeroDigits(handle, (int) MathF.Round(Speed), 3, 12, 52 + t, s, Text);
        DrawMiddle(handle, Label, Locale("luam-vehicle-dashboard-kmh"), 12 + digits + 3, 59 + t, s, Muted);

        var gearColor = Gear == "R" ? Warn : Gear == "D" ? Accent : Muted;
        Box(handle, 108, 38 + t, 22, 28, LitFill, gearColor, s);
        DrawMiddleCentered(handle, Digits, Gear, 119, 52 + t, s, gearColor);

        var fill = MaxSpeed > 0f ? Math.Clamp(Speed / MaxSpeed, 0f, 1f) : 0f;
        Segments(handle, 12, 90 + t, 120, 8, 20, fill, s, p => p > 0.85f ? Warn : Accent);
    }

    private void DrawTachBlock(DrawingHandleScreen handle, float s)
    {
        const float t = BodyTop - 24f;
        Box(handle, 142, 24 + t, 112, 82, Inset, SectionBorder, s);
        DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-dashboard-rpm"), 100, s), 148, 31 + t, s, Muted);
        var tenths = (int) MathF.Round(Math.Max(Tach, 0f) * 10f);
        DrawMiddleRight(handle, DigitsSmall, Format(TachFormat, tenths / 10, tenths % 10), 248, 44 + t, s, Text);
        Bars(handle, Math.Clamp(Tach / TachScaleMax, 0f, 1f), 100f + t, s);
    }

    // -- У ховера нет коробки и оборотов: вместо тахометра показывается тяга турбин в процентах
    private void DrawThrustBlock(DrawingHandleScreen handle, float s)
    {
        const float t = BodyTop - 24f;
        Box(handle, 142, 24 + t, 112, 82, Inset, SectionBorder, s);
        DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-dashboard-thrust"), 100, s), 148, 31 + t, s, Muted);
        var thrust = Math.Clamp(Thrust, 0f, 1f);
        DrawMiddleRight(handle, DigitsSmall, Format("{0}%", (int) MathF.Round(thrust * 100f)), 248, 44 + t, s, Text);
        Bars(handle, thrust, 100f + t, s);
    }

    private void Bars(DrawingHandleScreen handle, float fill, float baseY, float s)
    {
        const int count = 18;
        var lit = (int) MathF.Round(fill * count);
        for (var i = 0; i < count; i++)
        {
            var t = (i + 1f) / count;
            var h = 8f + 38f * t;
            var x = 148f + i * 5.6f;
            var color = t > 0.85f ? Danger : t > 0.65f ? Warn : Accent;
            handle.DrawRect(Rect(x, baseY - h, 4f, h, s), i < lit ? color : Off.WithAlpha(0.6f));
        }
    }

    private void DrawGaugesBlock(DrawingHandleScreen handle, float s)
    {
        const float t = BodyTop - 24f;
        Box(handle, 258, 24 + t, 116, 82, Inset, SectionBorder, s);
        Bar(handle, 264, 31 + t, Locale("luam-vehicle-dashboard-fuel"), Fuel, s);
        Bar(handle, 264, 69 + t, Locale("luam-vehicle-dashboard-hull"), Hull, s);
    }

    private void Bar(DrawingHandleScreen handle, float x, float middle, string label, float value, float s)
    {
        value = Math.Clamp(value, 0f, 1f);
        var color = value > 0.5f ? Accent : value > 0.2f ? Warn : Danger;
        var percentX = DrawMiddleRight(handle, DigitsSmall, Format("{0}%", (int) MathF.Round(value * 100f)), x + 104, middle, s, color);
        DrawMiddle(handle, Label, Fit(Label, label, percentX - x - 4f, s), x, middle, s, Muted);
        Segments(handle, x, middle + 7, 104, 12, 12, value, s, _ => color);
    }

    private float LimitToFraction(float limit)
    {
        var range = 1f - MinSpeedLimit;
        return range > 0f ? Math.Clamp((limit - MinSpeedLimit) / range, 0f, 1f) : 1f;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled || args.Function != EngineKeyFunctions.UIClick)
            return;

        var pos = args.RelativePosition / UIScale;
        if (pos.Y < LimitRowTop || pos.Y > LimitRowTop + LimitRowHeight || pos.X < TrackLeft - 8f || pos.X > TrackRight + 8f)
            return;

        _draggingLimit = true;
        SetLimitFromCursor(pos);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _draggingLimit = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_draggingLimit)
            SetLimitFromCursor(args.RelativePosition / UIScale);
    }

    // -- Шаг ползунка — 1 км/ч, чтобы не слать событие на каждый пиксель
    private void SetLimitFromCursor(Vector2 pos)
    {
        var fraction = Math.Clamp((pos.X - TrackLeft) / (TrackRight - TrackLeft), 0f, 1f);
        var limit = MinSpeedLimit + (1f - MinSpeedLimit) * fraction;
        if (MaxSpeed > 0f && limit < 1f)
            limit = MathF.Min(1f, MathF.Max(MinSpeedLimit, MathF.Round(MaxSpeed * limit) / MaxSpeed));

        if (MathHelper.CloseTo(limit, SpeedLimit, 0.0001f))
            return;

        SpeedLimit = limit;
        SpeedLimitChanged?.Invoke(limit);
    }
}
