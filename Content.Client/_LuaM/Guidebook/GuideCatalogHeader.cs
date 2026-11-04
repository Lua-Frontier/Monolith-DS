/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>A wrapped, icon-led catalogue row; no font-dependent arrow characters.</summary>
public sealed class GuideCatalogHeader : Button
{
    private readonly Label _toggle = new() { Text = "+", SetWidth = 20, VerticalAlignment = VAlignment.Center };
    public bool Expanded { set => _toggle.Text = value ? "-" : "+"; }
    public bool Expandable { set => _toggle.Visible = value; }
    public GuideCatalogHeader(string title, string? subtitle = null, Texture? icon = null)
    {
        HorizontalExpand = true;
        var row = new BoxContainer { HorizontalExpand = true, SeparationOverride = 8, Margin = new Thickness(8) };
        if (icon != null) row.AddChild(new TextureRect { Texture = icon, SetSize = new Vector2(44, 44),
            Stretch = TextureRect.StretchMode.KeepAspectCentered, VerticalAlignment = VAlignment.Center });
        var lines = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var heading = new RichTextLabel { HorizontalExpand = true };
        heading.SetMessage(FormattedMessage.FromMarkupPermissive("[bold]" + FormattedMessage.EscapeText(title) + "[/bold]"));
        lines.AddChild(heading);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var text = new RichTextLabel { HorizontalExpand = true };
            text.SetMessage(FormattedMessage.FromUnformatted(subtitle));
            lines.AddChild(text);
        }
        row.AddChild(lines); row.AddChild(_toggle); AddChild(row);
        DisableMouse(row);
    }
    private static void DisableMouse(Control control)
    {
        control.MouseFilter = MouseFilterMode.Ignore;
        foreach (var child in control.Children) DisableMouse(child);
    }
}
