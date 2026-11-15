// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Announcements;

/// <summary>Literal text with a cached full-message glyph layout. Revealing characters never parses or measures text.</summary>
public sealed class AnnouncementTextLabel : RichTextLabel
{
    private readonly record struct Glyph(Rune Rune, Vector2 Position, int End);
    private string _text = string.Empty;
    private readonly List<Glyph> _glyphs = new();
    private int[] _elements = [];
    private Font? _font;
    private float _width = -1;
    private float _scale;
    private float _lineHeight;
    private Vector2 _measured;
    private Color _color = Color.White;
    private int _lineCount;
    private float _lineHeightPixels;
    private int _cachedMaximumLines;
    public bool WrapText { get; set; } = true;
    public int MaximumLines { get; set; } = int.MaxValue;
    public bool FadeLastLine { get; set; }
    public Color TextColor => _color;

    public AnnouncementTextLabel()
    {
        VerticalAlignment = VAlignment.Top;
    }
    /// <summary>Explicit cached native font; defaults to the theme when omitted.</summary>
    private Font? _typeface;
    public Font? Typeface
    {
        get => _typeface;
        set
        {
            if (ReferenceEquals(_typeface, value)) return;
            _typeface = value;
            InvalidateMeasure();
        }
    }
    public int LayoutBuildCount { get; private set; }
    public int ElementCount => _elements.Length;
    public int VisibleElements { get; set; } = int.MaxValue;
    public float MinimumVisibleHeight { get; set; }
    public float MaximumVisibleHeight { get; set; } = float.PositiveInfinity;
    public int VisibleUtf16Length => VisibleElements <= 0 ? 0 : VisibleElements >= _elements.Length
        ? _text.Length : _elements[VisibleElements];

    public void SetText(string text, Color color)
    {
        _text = text;
        _color = color;
        // Keep combining marks, joined emoji and flag pairs intact using sandbox-allowed Rune APIs.
        var elements = new List<int>();
        var offset = 0;
        var joined = false;
        var regional = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var isRegional = rune.Value is >= 0x1F1E6 and <= 0x1F1FF;
            var continuation = joined || rune.Value == 0x200D
                || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
                || rune.Value is >= 0x1F3FB and <= 0x1F3FF
                || isRegional && regional % 2 == 1;
            if (!continuation || elements.Count == 0)
                elements.Add(offset);
            offset += rune.Utf16SequenceLength;
            joined = rune.Value == 0x200D;
            regional = isRegional ? regional + 1 : 0;
        }
        _elements = elements.ToArray();
        VisibleElements = _elements.Length;
        _width = -1;
        // Keep the normal literal-message API for accessibility and inspecting the complete text.
        SetMessage(FormattedMessage.FromUnformatted(text), color);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var font = Typeface ?? (TryGetStyleProperty<Font>("font", out var styled) ? styled : UserInterfaceManager.ThemeDefaults.DefaultFont);
        var width = WrapText ? Math.Max(1, availableSize.X * UIScale) : float.PositiveInfinity;
        if (_font == font && _width == width && _scale == UIScale && _lineHeight == LineHeightScale && _cachedMaximumLines == MaximumLines)
            return _measured;
        _font = font;
        _width = width;
        _scale = UIScale;
        _lineHeight = LineHeightScale;
        _cachedMaximumLines = MaximumLines;
        LayoutBuildCount++;
        _glyphs.Clear();
        var runes = _text.EnumerateRunes().ToArray();
        var advances = new int[runes.Length];
        for (var i = 0; i < runes.Length; i++)
            advances[i] = font.TryGetCharMetrics(runes[i], UIScale, out var metrics) ? metrics.Advance : 0;
        var x = 0f;
        var y = 0f;
        var maxX = 0f;
        var end = 0;
        var lineHeight = Math.Max(1, (int) (font.GetLineHeight(UIScale) * LineHeightScale));
        for (var i = 0; i < runes.Length; i++)
        {
            var rune = runes[i];
            end += rune.Utf16SequenceLength;
            if (rune.Value == '\n')
            {
                maxX = Math.Max(maxX, x);
                x = 0;
                y += lineHeight;
                continue;
            }
            // Wrap complete words when they fit; split exceptionally long words at glyph boundaries.
            if (!Rune.IsWhiteSpace(rune) && (i == 0 || Rune.IsWhiteSpace(runes[i - 1])))
            {
                var wordWidth = 0f;
                for (var j = i; j < runes.Length && !Rune.IsWhiteSpace(runes[j]); j++)
                    wordWidth += advances[j];
                if (x > 0 && x + wordWidth > width)
                {
                    maxX = Math.Max(maxX, x);
                    x = 0;
                    y += lineHeight;
                }
            }
            var advance = advances[i];
            if (x > 0 && x + advance > width)
            {
                maxX = Math.Max(maxX, x);
                x = 0;
                y += lineHeight;
            }
            if (x == 0 && Rune.IsWhiteSpace(rune))
                continue;
            _glyphs.Add(new Glyph(rune, new Vector2(x, y + font.GetAscent(UIScale)), end));
            x += advance;
        }
        maxX = Math.Max(maxX, x);
        _lineHeightPixels = lineHeight;
        _lineCount = (int) (y / lineHeight) + 1;
        _measured = _text.Length == 0 ? Vector2.Zero
            : new Vector2(Math.Min(width, maxX), (float) Math.Min(y + font.GetHeight(UIScale), (double) lineHeight * (MaximumLines - 1) + font.GetHeight(UIScale))) / UIScale;
        return _measured;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_font == null)
            return;
        var end = VisibleUtf16Length;
        var ascent = _font.GetAscent(UIScale);
        var firstBaseline = MinimumVisibleHeight * UIScale - _font.GetDescent(UIScale);
        var lastBaseline = MaximumVisibleHeight * UIScale + ascent;
        // Baselines are ordered. Jump over clipped paragraphs without scanning their glyphs
        // or asking the font for metrics on every frame while reading near the bottom.
        var first = 0;
        var last = _glyphs.Count;
        while (first < last)
        {
            var middle = first + (last - first) / 2;
            if (_glyphs[middle].Position.Y < firstBaseline) first = middle + 1;
            else last = middle;
        }
        for (var i = first; i < _glyphs.Count; i++)
        {
            var glyph = _glyphs[i];
            if (glyph.End > end)
                break;
            // A tall body is clipped by the card. Avoid submitting glyphs for all its hidden lines.
            if (glyph.Position.Y >= lastBaseline)
                break;
            var line = (int) MathF.Round((glyph.Position.Y - ascent) / _lineHeightPixels);
            if (line >= MaximumLines) break;
            var fade = FadeLastLine && _lineCount > MaximumLines && line == MaximumLines - 1
                ? Math.Clamp(1 - glyph.Position.X / Math.Max(1, _width), 0, 1) : 1;
            _font.DrawChar(handle, glyph.Rune, glyph.Position + new Vector2(UIScale), UIScale, Color.Black.WithAlpha(0.7f * fade));
            _font.DrawChar(handle, glyph.Rune, glyph.Position, UIScale, _color.WithAlpha(_color.A * fade));
        }
    }
}
