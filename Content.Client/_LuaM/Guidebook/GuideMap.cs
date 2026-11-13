/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
namespace Content.Client._LuaM.Guidebook;
public sealed class GuideMap : BoxContainer, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this; HorizontalExpand = true;
        if (!args.TryGetValue("Map", out var path)) return false;
        var name = args.GetValueOrDefault("Name", "Объект");
        var button = new Button { Text = "Открыть план: " + name, HorizontalExpand = true };
        button.OnPressed += _ => IoCManager.Resolve<IEntityManager>().System<GuidebookMapPreviewSystem>().Open(path, name);
        AddChild(button); return true;
    }
}
