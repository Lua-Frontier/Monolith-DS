/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Native heading fonts with wrapping, so long titles fit a narrow article.</summary>
public sealed class GuideHeading : RichTextLabel
{
    public GuideHeading(string text)
    {
        HorizontalExpand = true;
        ModulateSelfOverride = Color.FromHex("#55C7E2");
        SetMessage(FormattedMessage.FromUnformatted(text));
    }
}
