/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Content.Client.Guidebook.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

public sealed class GuideEntityCards : Container, IDocumentTag
{
    private int _columns = 1;
    private float[] _heights = Array.Empty<float>();
    private const float Padding = 12;
    private float InnerPadding => Frame ? Padding : 0;
    private const float Gap = 8;
    public float ItemWidth { get; set; } = 148;
    public bool Frame { get; set; } = true;
    private bool _flow;
    public GuideEntityCards()
    {
        HorizontalExpand = true;
        Margin = new Thickness(0, 6, 0, 14);
    }
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsFinite(availableSize.X) ? Math.Max(1, availableSize.X) : 640;
        var inner = Math.Max(1, width - InnerPadding * 2);
        _columns = Math.Clamp((int)((inner + Gap) / ItemWidth), 1, Math.Max(1, ChildCount));
        var column = Math.Max(1, (inner - Gap * (_columns - 1)) / _columns);
        _heights = new float[(ChildCount + _columns - 1) / _columns];
        var i = 0;
        foreach (var child in Children)
        {
            child.SetWidth = Math.Max(1, column - 8);
            child.Margin = new Thickness(4, 6);
            child.VerticalAlignment = VAlignment.Stretch;
            if (child is GuideEntityEmbed embed)
                embed.FindControl<SpriteView>("View").SetSize = new Vector2(64, 64);
            child.Measure(new Vector2(column, float.PositiveInfinity));
            _heights[i / _columns] = Math.Max(_heights[i / _columns], child.DesiredSize.Y);
            i++;
        }
        var height = InnerPadding * 2 + Math.Max(0, _heights.Length - 1) * Gap;
        foreach (var h in _heights) height += h;
        return new Vector2(width, height);
    }
    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var column = Math.Max(1, (finalSize.X - InnerPadding * 2 - Gap * (_columns - 1)) / _columns);
        var i = 0; var y = InnerPadding;
        foreach (var child in Children)
        {
            child.Arrange(UIBox2.FromDimensions(new Vector2(InnerPadding + (i % _columns) * (column + Gap), y),
                new Vector2(column, _heights[i / _columns])));
            i++;
            if (i % _columns == 0) y += _heights[(i - 1) / _columns] + Gap;
        }
        return finalSize;
    }
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (!Frame) return;
        handle.DrawRect(PixelSizeBox, Color.FromHex("#172833"));
        handle.DrawRect(new UIBox2(0, 0, PixelSize.X, 2 * UIScale), Color.FromHex("#31576B"));
        if (!_flow) return;
        var i = 0;
        foreach (var child in Children)
        {
            if (i++ % _columns == _columns - 1 || i == ChildCount) continue;
            var x = (child.Position.X + child.Size.X + 4) * UIScale;
            var y = (child.Position.Y + 28) * UIScale;
            var color = Color.FromHex("#73BFD3");
            handle.DrawLine(new Vector2(x - 4 * UIScale, y - 4 * UIScale), new Vector2(x, y), color);
            handle.DrawLine(new Vector2(x, y), new Vector2(x - 4 * UIScale, y + 4 * UIScale), color);
        }
    }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        _flow = args.GetValueOrDefault("Flow") == "true";
        control = this;
        return true;
    }
}
