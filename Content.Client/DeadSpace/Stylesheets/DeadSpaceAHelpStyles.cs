// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets;

/// <summary>
/// Visual rules used by the Dead Space 14 AHelp interface.
/// </summary>
public static class DeadSpaceAHelpStyles
{
    public const string Shell = "DS14MenuShell";
    public const string Header = "DS14MenuHeader";
    public const string Title = "DS14MenuTitle";
    public const string ProfileControl = "DS14MenuProfileControl";
    public const string ProfileControlDanger = "DS14MenuProfileControlDanger";
    public const string Input = "DS14MenuInput";
    public const string TextArea = "DS14MenuTextArea";

    public static StyleRule[] GetRules(IResourceCache resCache)
    {
        var shell = new StyleBoxTexture
        {
            Texture = resCache.GetTexture("/Textures/Interface/Nano/lobby_b.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        shell.SetPatchMargin(StyleBox.Margin.All, 24);
        shell.SetExpandMargin(StyleBox.Margin.All, -4);
        shell.SetContentMarginOverride(StyleBox.Margin.All, 10);

        var header = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#202631F5"),
            BorderColor = Color.FromHex("#5D6A7C"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
        };

        var profileControl = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#111923F0"),
            BorderColor = Color.FromHex("#2D4757"),
            BorderThickness = new Thickness(1),
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
            ContentMarginLeftOverride = 9,
            ContentMarginRightOverride = 9,
        };
        var profileControlHover = new StyleBoxFlat(profileControl)
        {
            BackgroundColor = Color.FromHex("#162638F4"),
            BorderColor = Color.FromHex("#1D7E9D"),
        };
        var profileControlPressed = new StyleBoxFlat(profileControl)
        {
            BackgroundColor = Color.FromHex("#18344AF5"),
            BorderColor = Color.FromHex("#2EA7D0"),
        };
        var profileControlDisabled = new StyleBoxFlat(profileControl)
        {
            BackgroundColor = Color.FromHex("#10161FC8"),
            BorderColor = Color.FromHex("#293844"),
        };
        var profileControlDangerHover = new StyleBoxFlat(profileControl)
        {
            BackgroundColor = Color.FromHex("#431E25F6"),
            BorderColor = Color.FromHex("#F85149"),
        };

        var input = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#0D1219F6"),
            BorderColor = Color.FromHex("#2D4757"),
            BorderThickness = new Thickness(1),
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
            ContentMarginLeftOverride = 7,
            ContentMarginRightOverride = 7,
        };
        var inputDisabled = new StyleBoxFlat(input)
        {
            BackgroundColor = Color.FromHex("#10161FC8"),
            BorderColor = Color.FromHex("#293844"),
        };
        var textArea = new StyleBoxFlat(input)
        {
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
            ContentMarginLeftOverride = 7,
            ContentMarginRightOverride = 7,
        };

        return
        [
            Element<PanelContainer>().Class(Shell)
                .Prop(PanelContainer.StylePropertyPanel, shell),
            Element<PanelContainer>().Class(Header)
                .Prop(PanelContainer.StylePropertyPanel, header),
            Element<OutputPanel>().Class(TextArea)
                .Prop(OutputPanel.StylePropertyStyleBox, textArea),
            Element<LineEdit>().Class(Input)
                .Prop(LineEdit.StylePropertyStyleBox, input)
                .Prop("font-color", Color.FromHex("#F1F3F6")),
            Element<LineEdit>().Class(Input).Class(LineEdit.StyleClassLineEditNotEditable)
                .Prop(LineEdit.StylePropertyStyleBox, inputDisabled)
                .Prop("font-color", Color.FromHex("#9BA6AD")),
            Element<LineEdit>().Class(Input).Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", Color.FromHex("#7A8590")),
            Element<Label>().Class(Title)
                .Prop(Label.StylePropertyFont, resCache.NotoStack(variation: "Bold", size: 16))
                .Prop(Label.StylePropertyFontColor, Color.FromHex("#4EC6E8")),
            ButtonRule(ProfileControl, null, profileControl),
            ButtonRule(ProfileControl, ContainerButton.StylePseudoClassHover, profileControlHover),
            ButtonRule(ProfileControl, ContainerButton.StylePseudoClassPressed, profileControlPressed),
            ButtonRule(ProfileControl, ContainerButton.StylePseudoClassDisabled, profileControlDisabled),
            ButtonRule(ProfileControlDanger, null, profileControl),
            ButtonRule(ProfileControlDanger, ContainerButton.StylePseudoClassHover, profileControlDangerHover),
            ButtonRule(ProfileControlDanger, ContainerButton.StylePseudoClassPressed, profileControlDangerHover),
            ButtonRule(ProfileControlDanger, ContainerButton.StylePseudoClassDisabled, profileControlDisabled),
        ];
    }

    private static StyleRule ButtonRule(string styleClass, string? pseudoClass, StyleBox styleBox)
    {
        var selector = Element<Button>().Class(styleClass);
        if (pseudoClass != null)
            selector = selector.Pseudo(pseudoClass);

        return selector.Prop(ContainerButton.StylePropertyStyleBox, styleBox);
    }
}
