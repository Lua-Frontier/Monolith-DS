using Content.Client.UserInterface.Controls;

namespace Content.Client._Lua.Styles;

[Virtual]
public class LunaWindow : FancyWindow
{
    protected void ApplyLunaChrome()
    {
        LunaWindowStyle.ApplyWindowChrome(this);
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();
        LunaWindowStyle.ApplyWindowChrome(this);
    }
}
