/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Stable column widths across every row, including headings; cells align top-left.</summary>
public sealed class GuideTable : Container, IDocumentTag
{
    private int _columns = 2;
    private float[] _heights = Array.Empty<float>();
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        if (int.TryParse(args.GetValueOrDefault("Columns"), out var count)) _columns = Math.Max(1, count);
        HorizontalExpand = true; control = this; return true;
    }
    private new float Width(float total, int col) => _columns == 2 ? total * (col == 0 ? 0.34f : 0.66f) : total / _columns;
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsFinite(availableSize.X) ? availableSize.X : 540;
        _heights = new float[(ChildCount + _columns - 1) / _columns];
        var i = 0;
        foreach (var cell in Children)
        {
            cell.HorizontalAlignment = HAlignment.Stretch; cell.VerticalAlignment = VAlignment.Top;
            cell.Margin = new Thickness(8, 6);
            cell.Measure(new Vector2(Width(width, i % _columns), float.PositiveInfinity));
            _heights[i / _columns] = Math.Max(_heights[i / _columns], cell.DesiredSize.Y); i++;
        }
        var height = 0f; foreach (var h in _heights) height += h;
        return new Vector2(width, height);
    }
    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var i = 0; var x = 0f; var y = 0f;
        foreach (var cell in Children)
        {
            var width = Width(finalSize.X, i % _columns); var height = _heights[i / _columns];
            cell.Arrange(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(width, height)));
            x += width; i++; if (i % _columns == 0) { x = 0; y += height; }
        }
        return finalSize;
    }
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var y = 0f;
        for (var row = 0; row < _heights.Length; row++)
        {
            var height = _heights[row] * UIScale;
            handle.DrawRect(new UIBox2(0, y, PixelSize.X, y + height),
                Color.FromHex(row == 0 ? "#23404F" : row % 2 == 0 ? "#162731" : "#12212B"));
            handle.DrawRect(new UIBox2(0, y + height - 1, PixelSize.X, y + height), Color.FromHex("#2B414D"));
            y += height;
        }
    }
}
