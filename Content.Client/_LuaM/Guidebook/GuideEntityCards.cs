/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

public sealed class GuideEntityCards : GridContainer, IDocumentTag
{
    public GuideEntityCards()
    {
        HorizontalExpand = true;
        MaxGridWidth = 540;
        HSeparationOverride = 4;
        VSeparationOverride = 6;
    }

    private float _layoutWidth = 540;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Keep galleries readable even when a parent offers the whole screen width.
        // Four fixed-size cards always form at least two rows rather than a thin strip.
        var width = float.IsFinite(availableSize.X) && availableSize.X > 0 ? Math.Min(540, availableSize.X) : 540;
        if (_layoutWidth != width)
            MaxGridWidth = _layoutWidth = width;
        return base.MeasureOverride(availableSize);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        return true;
    }
}
