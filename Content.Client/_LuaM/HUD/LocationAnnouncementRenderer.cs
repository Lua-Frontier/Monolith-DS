using System.Numerics;
using System.Text;
using Content.Shared._LuaM.Announcements;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._LuaM.Hud;

/// <summary>Shared pixel-space rendering for the announcement and its editor preview.</summary>
public sealed class LocationAnnouncementRenderer
{
    private readonly Font _title;
    private readonly Font _description;
    private string? _lastTitle;
    private string? _lastDescription;
    private Vector2 _titleSize;
    private Vector2 _descriptionSize;

    public LocationAnnouncementRenderer(IResourceCache cache)
    {
        _title = cache.GetFont("/Fonts/Jura/Jura.ttf", 25);
        _description = cache.GetFont("/Fonts/Jura/Jura.ttf", 15);
    }

    private static Vector2 Measure(Font font, string text)
    {
        var width = 0f;
        var maxWidth = 0f;
        var lines = 1;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune == new Rune('\n'))
            {
                maxWidth = Math.Max(maxWidth, width);
                width = 0;
                lines++;
            }
            else if (font.TryGetCharMetrics(rune, 1, out var metrics))
                width += metrics.Advance;
        }
        return new Vector2(Math.Max(maxWidth, width), lines * font.GetLineHeight(1));
    }

    public Vector2 GetSize(string title, string description)
    {
        if (_lastTitle != title)
        {
            _lastTitle = title;
            _titleSize = Measure(_title, title);
        }
        if (_lastDescription != description)
        {
            _lastDescription = description;
            _descriptionSize = Measure(_description, description);
        }
        return new Vector2(Math.Max(_titleSize.X, _descriptionSize.X),
            _titleSize.Y + (description.Length > 0 ? 40 + _descriptionSize.Y : 0));
    }

    public UIBox2 GetBounds(Vector2 screen, Vector2 anchor, string title, string description, HudHorizontalAlignment alignment = HudHorizontalAlignment.Center, float requestedScale = 1)
    {
        var size = GetSize(title, description);
        var scale = Math.Min(HudLayout.NormalizeScale(requestedScale), Math.Min(screen.X / Math.Max(size.X, 1), screen.Y / Math.Max(size.Y, 1)));
        size *= Math.Max(0, scale);
        return UIBox2.FromDimensions(HudLayout.Position(screen, size, anchor, alignment), size);
    }

    public void Draw(DrawingHandleScreen handle, Vector2 screen, Vector2 anchor,
        string title, string description, int titleLength, int descriptionLength, HudHorizontalAlignment alignment = HudHorizontalAlignment.Center, float requestedScale = 1)
    {
        var size = GetSize(title, description);
        var bounds = GetBounds(screen, anchor, title, description, alignment, requestedScale);
        var scale = size.Y > 0 ? bounds.Height / size.Y : 1;
        DrawText(handle, _title, title, titleLength,
            bounds.TopLeft + new Vector2((bounds.Width - _titleSize.X * scale) * HudLayout.HorizontalFraction(alignment), 0), scale, Color.White);
        DrawText(handle, _description, description, descriptionLength,
            bounds.TopLeft + new Vector2((bounds.Width - _descriptionSize.X * scale) * HudLayout.HorizontalFraction(alignment),
                (_titleSize.Y + 40) * scale), scale, Color.DarkGray);
    }

    private static void DrawText(DrawingHandleScreen handle, Font font, string text, int length,
        Vector2 position, float scale, Color color)
    {
        var cursor = position + new Vector2(0, font.GetAscent(scale));
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            index += rune.Utf16SequenceLength;
            if (index > length)
                break;
            if (rune == new Rune('\n'))
                cursor = new Vector2(position.X, cursor.Y + font.GetLineHeight(scale));
            else
                cursor.X += font.DrawChar(handle, rune, cursor, scale, color);
        }
    }
}
