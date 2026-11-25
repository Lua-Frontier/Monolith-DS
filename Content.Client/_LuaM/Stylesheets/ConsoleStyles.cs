using Content.Client._LuaM.UserInterface.Hud;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets;

public static class ConsoleStyles
{
    public const string Section = "LuaMConsoleSection";
    public const string Inset = "LuaMConsoleInset";
    public const string Header = "LuaMConsoleHeader";
    public const string HeaderLabel = "LuaMConsoleHeaderLabel";
    public const string Button = "LuaMConsoleButton";
    public const string Input = "LuaMConsoleInput";

    public const string Blue = "LuaMLampBlue";
    public const string Green = "LuaMLampGreen";
    public const string Yellow = "LuaMLampYellow";
    public const string Red = "LuaMLampRed";
    public const string Violet = "LuaMLampViolet";
    public const string Orange = "LuaMLampOrange";

    public static readonly (string Class, Color Color)[] Lamps =
    {
        (Blue, HudPalette.Advisory),
        (Green, HudPalette.Good),
        (Yellow, HudPalette.Caution),
        (Red, HudPalette.Bad),
        (Violet, HudAccents.Science),
        (Orange, HudAccents.Cargo),
    };

    public static StyleRule[] GetRules(IResourceCache resCache)
    {
        var section = StyleBoxHud.Frame(HudPalette.Panel.WithAlpha(0.96f), null, 0);
        section.Chamfer = 6;
        var inset = new StyleBoxGrid { Step = 0 };
        var header = new StyleBoxHudHeader { Inset = 0, Chamfer = 0 };
        header.SetContentMarginOverride(StyleBox.Margin.Vertical, 5);
        header.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);

        var input = HudThemeStyles.Flat(HudPalette.Background, HudPalette.Border);
        input.SetContentMarginOverride(StyleBox.Margin.Vertical, 4);
        input.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);

        var legend = StyleBoxLamp.Mix(HudPalette.Fg, HudPalette.Text, 0.45f);
        var font = LobbyHudStyles.UiFont(resCache, 13, "Medium");

        var rules = new List<StyleRule>
        {
            Element<PanelContainer>().Class(Section)
                .Prop(PanelContainer.StylePropertyPanel, section),
            Element<PanelContainer>().Class(Inset)
                .Prop(PanelContainer.StylePropertyPanel, inset),
            Element<PanelContainer>().Class(Header)
                .Prop(PanelContainer.StylePropertyPanel, header),
            Element<Label>().Class(HeaderLabel)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoMediumFont(resCache, 12))
                .Prop(Label.StylePropertyFontColor, HudPalette.Advisory),

            Element<LineEdit>().Class(Input)
                .Prop(LineEdit.StylePropertyStyleBox, input)
                .Prop("font-color", HudPalette.Text),
            Element<LineEdit>().Class(Input).Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", HudPalette.TextDim),
        };

        var shell = HudThemeStyles.WindowFrame(null);
        shell.SetContentMarginOverride(StyleBox.Margin.All, 10);
        var titleHeader = HudThemeStyles.Header(null, true);
        titleHeader.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        var textArea = HudThemeStyles.Flat(HudPalette.Background, HudPalette.Border);
        textArea.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        textArea.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);
        var inputDisabled = HudThemeStyles.Flat(HudPalette.Panel, HudPalette.LineDim);
        inputDisabled.SetContentMarginOverride(StyleBox.Margin.Vertical, 4);
        inputDisabled.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);

        rules.AddRange(new StyleRule[]
        {
            Element<PanelContainer>().Class(DeadSpaceAHelpStyles.Shell)
                .Prop(PanelContainer.StylePropertyPanel, shell),
            Element<PanelContainer>().Class(DeadSpaceAHelpStyles.Header)
                .Prop(PanelContainer.StylePropertyPanel, titleHeader),
            Element<Label>().Class(DeadSpaceAHelpStyles.Title)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoMediumFont(resCache, 12))
                .Prop(Label.StylePropertyFontColor, HudPalette.Fg),
            Element<OutputPanel>().Class(DeadSpaceAHelpStyles.TextArea)
                .Prop(OutputPanel.StylePropertyStyleBox, textArea),
            Element<LineEdit>().Class(DeadSpaceAHelpStyles.Input)
                .Prop(LineEdit.StylePropertyStyleBox, input)
                .Prop("font-color", HudPalette.Text),
            Element<LineEdit>().Class(DeadSpaceAHelpStyles.Input).Class(LineEdit.StyleClassLineEditNotEditable)
                .Prop(LineEdit.StylePropertyStyleBox, inputDisabled)
                .Prop("font-color", HudPalette.TextDim),
            Element<LineEdit>().Class(DeadSpaceAHelpStyles.Input).Pseudo(LineEdit.StylePseudoClassPlaceholder)
                .Prop("font-color", HudPalette.TextDim),
        });

        AddLamp(rules, Button, HudPalette.Fg, legend, font, 1);
        AddLamp(rules, DeadSpaceAHelpStyles.ProfileControl, HudPalette.Fg, legend, font, 1);
        AddLamp(rules, DeadSpaceAHelpStyles.ProfileControlDanger, HudPalette.Bad, StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Text, 0.6f), font, 1);
        foreach (var (cls, color) in Lamps)
        {
            AddLamp(rules, cls, color, StyleBoxLamp.Mix(color, HudPalette.Text, 0.6f), font, 2);
        }

        return rules.ToArray();
    }

    private static void AddLamp(List<StyleRule> rules, string cls, Color color, Color text, Font font, int weight)
    {
        var fg = color == HudPalette.Fg;
        var normal = HudThemeStyles.Lamp(color, StyleBoxLamp.Lit.Off, fg ? null : StyleBoxLamp.Mix(color, HudPalette.Border, 0.4f));
        var hover = HudThemeStyles.Lamp(color, StyleBoxLamp.Lit.Off, fg ? HudPalette.BorderBright : color);
        var pressed = HudThemeStyles.Lamp(color, fg ? StyleBoxLamp.Lit.On : StyleBoxLamp.Lit.Fill, fg ? HudPalette.Fg : null);
        var disabled = HudThemeStyles.Lamp(HudPalette.Offline, StyleBoxLamp.Lit.Off, HudPalette.LineDim);

        foreach (var (pseudo, box, label) in new[]
                 {
                     (ContainerButton.StylePseudoClassNormal, (StyleBox) normal, text),
                     (ContainerButton.StylePseudoClassHover, hover, color),
                     (ContainerButton.StylePseudoClassPressed, pressed, fg ? color : HudPalette.Void),
                     (ContainerButton.StylePseudoClassDisabled, disabled, HudPalette.Offline),
                 })
        {
            var selector = Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(cls);
            rules.Add(selector.Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, box)
                .Prop(Control.StylePropertyModulateSelf, Color.White));

            var classes = new[] { cls };
            rules.Add(new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(ContainerButton), classes, null, new[] { pseudo }),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFontColor, label), new StyleProperty(Label.StylePropertyFont, font) }));
        }
    }
}
