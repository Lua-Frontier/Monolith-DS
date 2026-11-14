using System.Numerics;

namespace Content.Client._LuaM.Hud;

/// <summary>Normalized top-center anchor; description size must not move the title.</summary>
public static class HudLayout
{
    public static readonly Vector2 DefaultPosition = new(0.5f, 0.15f);

    public static Vector2 Normalize(Vector2 value) => new(
        float.IsFinite(value.X) ? Math.Clamp(value.X, 0, 1) : DefaultPosition.X,
        float.IsFinite(value.Y) ? Math.Clamp(value.Y, 0, 1) : DefaultPosition.Y);

    public static Vector2 Position(Vector2 screen, Vector2 block, Vector2 anchor)
    {
        var point = screen * Normalize(anchor) - new Vector2(block.X / 2, 0);
        return Vector2.Clamp(point, Vector2.Zero, Vector2.Max(screen - block, Vector2.Zero));
    }

    public static Vector2 FromPosition(Vector2 screen, Vector2 block, Vector2 position)
    {
        var point = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(screen - block, Vector2.Zero));
        return Normalize(new Vector2(screen.X > 0 ? (point.X + block.X / 2) / screen.X : 0,
            screen.Y > 0 ? point.Y / screen.Y : 0));
    }
}
