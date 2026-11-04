/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Guidebook.Richtext;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Responsive flow diagram, using text from the manuscript and vector connectors.</summary>
public sealed class GuideGasCircuit : BoxContainer, IDocumentTag
{
    public GuideGasCircuit() { Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; Margin = new Thickness(8); }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        foreach (var (key, title) in new[] { ("Inputs", "Баллоны и порты"), ("Mixer", "Смеситель и насос"),
                     ("Chamber", "Реакционный контур"), ("Product", "Фильтр и выход") })
        {
            if (!args.TryGetValue(key, out var text)) continue;
            if (ChildCount > 0) AddChild(new Connector());
            var panel = new PanelContainer { HorizontalExpand = true, PanelOverride = new StyleBoxFlat(Color.FromHex("#182B37")) };
            var label = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(10, 7) };
            label.SetMessage(FormattedMessage.FromMarkupPermissive("[bold]" + title + "[/bold]\n" + FormattedMessage.EscapeText(text)));
            panel.AddChild(label); AddChild(panel);
        }
        return true;
    }
    private sealed class Connector : Control
    {
        public Connector() { SetHeight = 18; HorizontalExpand = true; MouseFilter = MouseFilterMode.Ignore; }
        protected override void Draw(DrawingHandleScreen handle)
        {
            var middle = PixelWidth / 2f; var bottom = PixelHeight - 2;
            var color = Color.FromHex("#59C6DF");
            handle.DrawLine(new Vector2(middle, 0), new Vector2(middle, bottom), color);
            handle.DrawLine(new Vector2(middle - 4, bottom - 5), new Vector2(middle, bottom), color);
            handle.DrawLine(new Vector2(middle + 4, bottom - 5), new Vector2(middle, bottom), color);
        }
    }
}
