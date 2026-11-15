/* LuaM - AGPLv3. See AGPLv3.txt. */
using Robust.Shared.Configuration;

namespace Content.Shared._LuaM.Guidebook;

[CVarDefs]
public sealed class GuidebookCVars
{
    public static readonly CVarDef<float> WindowWidth =
        CVarDef.Create("guidebook.window_width", 900f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> WindowHeight =
        CVarDef.Create("guidebook.window_height", 700f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> NavigationWidth =
        CVarDef.Create("guidebook.navigation_width", 240f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
