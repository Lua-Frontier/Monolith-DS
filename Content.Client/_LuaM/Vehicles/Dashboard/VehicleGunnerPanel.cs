using Robust.Client.Graphics;

namespace Content.Client._LuaM.Vehicles.Dashboard;

public sealed class VehicleGunnerPanel : VehicleHudPanel
{
    private const float RowSpacing = 12f;
    private const int MaxRows = 3;
    private const float RightX = 109f;
    private const float RightEdge = 248f;

    public int Ammo;
    public int Capacity;
    public float Reload = 1f;
    public float Azimuth;
    public readonly List<(string NameId, int Count, int Capacity)> Shells = new();
    public int Selected = -1;
    public string Weapon = string.Empty;

    public VehicleGunnerPanel() : base(260f, 90f)
    {
    }

    protected override float DrawHeader(DrawingHandleScreen handle, float right, float s)
    {
        var (status, color) = Ammo <= 0
            ? (Locale("luam-vehicle-gunner-empty"), Danger)
            : Reload < 1f
                ? (Locale("luam-vehicle-gunner-reloading"), Warn)
                : (Locale("luam-vehicle-gunner-ready"), Good);

        return StatusLight(handle, right, status, color, s);
    }

    protected override void DrawBody(DrawingHandleScreen handle, float s)
    {
        DrawAmmoBlock(handle, s);
        DrawRightBlock(handle, s);
    }

    private void DrawAmmoBlock(DrawingHandleScreen handle, float s)
    {
        Box(handle, 6, 24, 94, 60, Inset, SectionBorder, s);
        DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-gunner-ammo"), 84, s), 11, 31, s, Muted);

        var digits = Capacity >= 100 || Ammo >= 100 ? 3 : 2;
        var ammoColor = Ammo <= 0 ? Danger : Capacity > 0 && Ammo <= Capacity / 4 ? Warn : Text;
        var advance = LeadingZeroDigits(handle, Ammo, digits, 11, 51, s, ammoColor);

        var capacity = Format("/{0}", Capacity);
        var capacityX = 11 + advance + 2;
        if (Capacity > 0 && capacityX + Width(DigitsSmall, capacity, s) <= 95)
            DrawMiddle(handle, DigitsSmall, capacity, capacityX, 56, s, Muted);

        var fill = Math.Clamp(Reload, 0f, 1f);
        Segments(handle, 11, 74, 84, 4, 12, fill, s, _ => fill >= 1f ? Good : Warn);
    }

    private void DrawRightBlock(DrawingHandleScreen handle, float s)
    {
        Box(handle, 104, 24, 150, 60, Inset, SectionBorder, s);

        if (Shells.Count > 0)
        {
            for (var i = 0; i < Shells.Count && i < MaxRows; i++)
            {
                var (nameId, count, capacity) = Shells[i];
                var middle = 32 + i * RowSpacing;
                var selected = i == Selected;
                if (selected)
                {
                    Box(handle, 107, middle - 5.5f, 144, 11, LitFill, null, s);
                    handle.DrawRect(Rect(107, middle - 5.5f, 2, 11, s), Accent);
                }

                var textColor = selected ? Accent : count > 0 ? Text : Muted;
                var countX = DrawMiddleRight(handle, DigitsSmall, Format("{0:00}/{1:00}", count, capacity), RightEdge, middle, s, count > 0 ? textColor : Danger);
                DrawMiddle(handle, Label, Fit(Label, Upper(Locale(nameId)), countX - 4f - (RightX + 3), s), RightX + 3, middle, s, textColor);
            }
        }
        else
        {
            DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-gunner-weapon"), RightEdge - RightX, s), RightX, 32, s, Muted);
            DrawMiddle(handle, TitleFont, Fit(TitleFont, Upper(Weapon), RightEdge - RightX, s), RightX, 47, s, Text);
        }

        handle.DrawRect(Rect(107, 67, 144, 1, s), SectionBorder);
        var heading = ((int) MathF.Round(Azimuth) % 360 + 360) % 360;
        var valueX = DrawMiddleRight(handle, DigitsSmall, Format("{0:000}°", heading), RightEdge, 75, s, Accent);
        DrawMiddle(handle, Label, Fit(Label, Locale("luam-vehicle-gunner-azimuth"), valueX - 4f - RightX, s), RightX, 75, s, Muted);
    }
}
