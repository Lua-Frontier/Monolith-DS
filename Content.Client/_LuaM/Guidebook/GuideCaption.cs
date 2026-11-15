/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Plain item names wrapped into individually centered lines.</summary>
public sealed class GuideCaption : RichTextLabel
{
    private readonly List<Label> _lines = new();
    private string? _previous;
    private float _width = -1;
    private float _scale;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var text = GetFormattedMessage()?.ToString() ?? "";
        var width = float.IsFinite(availableSize.X) ? Math.Max(1, availableSize.X) : 170;
        if (_previous != text || _width != width || _scale != UIScale)
        {
            foreach (var line in _lines) line.Dispose();
            _lines.Clear();
            _previous = text;
            _width = width;
            _scale = UIScale;
            using var probe = new Label();
            AddChild(probe); // Measure with the same inherited stylesheet and font as the visible lines.
            var current = "";
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                probe.Text = candidate;
                probe.Measure(new Vector2(float.PositiveInfinity));
                if (probe.DesiredSize.X > width)
                {
                    if (current.Length > 0) AddLine(current);
                    current = "";
                    foreach (var letter in word)
                    {
                        // Explicit string overload avoids the compiler's sandbox-incompatible span concatenation.
                        var next = string.Concat(current, letter.ToString());
                        probe.Text = next;
                        probe.Measure(new Vector2(float.PositiveInfinity));
                        if (current.Length > 0 && probe.DesiredSize.X > width)
                        {
                            AddLine(current);
                            current = "";
                            next = letter.ToString();
                        }
                        current = next;
                    }
                }
                else current = candidate;
            }
            if (current.Length > 0) AddLine(current);
        }
        var height = 0f;
        foreach (var line in _lines)
        {
            line.Measure(new Vector2(width, float.PositiveInfinity));
            height += line.DesiredSize.Y;
        }
        return new Vector2(width, height);
    }

    private void AddLine(string text)
    {
        var line = new Label { Text = text, Align = Label.AlignMode.Center, HorizontalExpand = true };
        _lines.Add(line);
        AddChild(line);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var y = 0f;
        foreach (var line in _lines)
        {
            line.Arrange(UIBox2.FromDimensions(new Vector2(0, y), new Vector2(finalSize.X, line.DesiredSize.Y)));
            y += line.DesiredSize.Y;
        }
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle) { }

    protected override void StylePropertiesChanged()
    {
        _width = -1;
        base.StylePropertiesChanged();
    }
}
