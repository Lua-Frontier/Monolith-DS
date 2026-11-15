using Robust.Client.Graphics;

namespace Content.Client._LuaM.Vehicles.Dashboard;

public sealed class VehicleDashboard : VehicleHudPanel
{
    private const float TachScaleMax = 7f;

    public float Tach;
    public float Fuel;
    public float Speed;
    public float MaxSpeed = 60f;
    public float Hull = 1f;
    public bool EngineOn;
    public bool EngineStarting;
    public bool Lights;
    public bool Handbrake;
    public bool Boost;
    public bool HasFuel = true;
    public char Gear = 'N';

    public VehicleDashboard() : base(380f, 112f)
    {
    }

    protected override float DrawHeader(DrawingHandleScreen handle, float right, float s)
    {
        var (status, color) = !HasFuel
            ? (Loc.GetString("luam-vehicle-dashboard-no-fuel"), Danger)
            : EngineStarting
                ? (Loc.GetString("luam-vehicle-dashboard-starting"), Warn)
                : EngineOn
                    ? (Loc.GetString("luam-vehicle-dashboard-running"), Good)
                    : (Loc.GetString("luam-vehicle-dashboard-off"), Muted);

        var x = StatusLight(handle, right, status, color, s) - 6f;
        x = HeaderIndicator(handle, x, Loc.GetString("luam-vehicle-dashboard-boost"), Boost ? Warn : null, s);
        x = HeaderIndicator(handle, x, Loc.GetString("luam-vehicle-dashboard-handbrake"), Handbrake ? Danger : null, s);
        x = HeaderIndicator(handle, x, Loc.GetString("luam-vehicle-dashboard-lights"), Lights ? Accent : null, s);
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
        DrawSpeedBlock(handle, s);
        DrawTachBlock(handle, s);
        DrawGaugesBlock(handle, s);
    }

    private void DrawSpeedBlock(DrawingHandleScreen handle, float s)
    {
        Box(handle, 6, 24, 132, 82, Inset, SectionBorder, s);
        DrawMiddle(handle, Label, Fit(Label, Loc.GetString("luam-vehicle-dashboard-speed"), 120, s), 12, 31, s, Muted);

        var digits = LeadingZeroDigits(handle, (int) MathF.Round(Speed), 3, 12, 52, s, Text);
        DrawMiddle(handle, Label, Loc.GetString("luam-vehicle-dashboard-kmh"), 12 + digits + 3, 59, s, Muted);

        var gearColor = Gear == 'R' ? Warn : Gear == 'D' ? Accent : Muted;
        Box(handle, 108, 38, 22, 28, LitFill, gearColor, s);
        DrawMiddleCentered(handle, Digits, Gear.ToString(), 119, 52, s, gearColor);

        var fill = MaxSpeed > 0f ? Math.Clamp(Speed / MaxSpeed, 0f, 1f) : 0f;
        Segments(handle, 12, 90, 120, 8, 20, fill, s, t => t > 0.85f ? Warn : Accent);
    }

    private void DrawTachBlock(DrawingHandleScreen handle, float s)
    {
        Box(handle, 142, 24, 112, 82, Inset, SectionBorder, s);
        DrawMiddle(handle, Label, Fit(Label, Loc.GetString("luam-vehicle-dashboard-rpm"), 100, s), 148, 31, s, Muted);
        DrawMiddleRight(handle, DigitsSmall, Tach.ToString("0.0"), 248, 44, s, Text);

        const int count = 18;
        var fill = Math.Clamp(Tach / TachScaleMax, 0f, 1f);
        var lit = (int) MathF.Round(fill * count);
        var baseY = 100f;
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
        Box(handle, 258, 24, 116, 82, Inset, SectionBorder, s);
        Bar(handle, 264, 31, Loc.GetString("luam-vehicle-dashboard-fuel"), Fuel, s);
        Bar(handle, 264, 69, Loc.GetString("luam-vehicle-dashboard-hull"), Hull, s);
    }

    private void Bar(DrawingHandleScreen handle, float x, float middle, string label, float value, float s)
    {
        value = Math.Clamp(value, 0f, 1f);
        var color = value > 0.5f ? Accent : value > 0.2f ? Warn : Danger;
        var percentX = DrawMiddleRight(handle, DigitsSmall, $"{(int) MathF.Round(value * 100f)}%", x + 104, middle, s, color);
        DrawMiddle(handle, Label, Fit(Label, label, percentX - x - 4f, s), x, middle, s, Muted);
        Segments(handle, x, middle + 7, 104, 12, 12, value, s, _ => color);
    }
}
