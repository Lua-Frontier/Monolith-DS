using System.Numerics;
using Content.Shared._LuaM.Announcements;
using Content.Shared._LuaM.CCVar;

namespace Content.Client._LuaM.Hud;

/// <summary>Normalized horizontal-edge anchor; description size must not move the title.</summary>
public static class HudLayout
{
    public const float MinimumScale = 0.65f;
    public static float NormalizeScale(float value, float maximum = 1.5f) =>
        float.IsFinite(value) ? Math.Clamp(value, MinimumScale, maximum) : 1f;
    public static float WheelScale(float value, float delta, float maximum = 1.5f) =>
        NormalizeScale(NormalizeScale(value, maximum) + (float.IsFinite(delta) ? Math.Clamp(delta, -5, 5) * 0.05f : 0), maximum);

    public static readonly Vector2 DefaultPosition = new(LuaMCVars.DefaultLocationAnnouncementX, LuaMCVars.DefaultLocationAnnouncementY);
    public static readonly Vector2 DefaultScreenPosition = new(LuaMCVars.DefaultScreenAnnouncementX, LuaMCVars.DefaultScreenAnnouncementY);

    public static Vector2 Normalize(Vector2 value) => new(
        float.IsFinite(value.X) ? Math.Clamp(value.X, 0, 1) : DefaultPosition.X,
        float.IsFinite(value.Y) ? Math.Clamp(value.Y, 0, 1) : DefaultPosition.Y);

    public static HudHorizontalAlignment NormalizeAlignment(HudHorizontalAlignment alignment) =>
        alignment is HudHorizontalAlignment.Left or HudHorizontalAlignment.Right ? alignment : HudHorizontalAlignment.Center;

    public static float HorizontalFraction(HudHorizontalAlignment alignment) => (int) NormalizeAlignment(alignment) / 2f;

    public static Vector2 Position(Vector2 screen, Vector2 block, Vector2 anchor, HudHorizontalAlignment alignment = HudHorizontalAlignment.Center, bool allowHorizontalOverflow = false)
    {
        var point = screen * Normalize(anchor) - new Vector2(block.X * HorizontalFraction(alignment), 0);
        var clamped = Vector2.Clamp(point, Vector2.Zero, Vector2.Max(screen - block, Vector2.Zero));
        return allowHorizontalOverflow ? new Vector2(point.X, clamped.Y) : clamped;
    }

    public static Vector2 FromPosition(Vector2 screen, Vector2 block, Vector2 position, HudHorizontalAlignment alignment = HudHorizontalAlignment.Center, bool allowHorizontalOverflow = false)
    {
        var point = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(screen - block, Vector2.Zero));
        if (allowHorizontalOverflow) point.X = position.X;
        return Normalize(new Vector2(screen.X > 0 ? (point.X + block.X * HorizontalFraction(alignment)) / screen.X : 0,
            screen.Y > 0 ? point.Y / screen.Y : 0));
    }
}
