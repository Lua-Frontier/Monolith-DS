using System.Linq;
using System.Numerics;
using Content.Shared._Mono.ShipGuns;
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
    public const string Thin = "LuaMConsoleThin";
    public const string Toggle = "LuaMConsoleToggle";
    public const string Caption = "LuaMConsoleCaption";
    public const string Key = "LuaMConsoleKey";
    public const string Value = "LuaMConsoleValue";
    public const string Readout = "LuaMConsoleReadout";
    public const string ReadoutUnit = "LuaMConsoleReadoutUnit";
    public const string GunBallistic = "LuaMGunBallistic";
    public const string GunEnergy = "LuaMGunEnergy";
    public const string GunMissile = "LuaMGunMissile";
    public const string Pick = "Pick";
    public const string Hazard = "LuaMConsoleHazard";
    public const string HazardLabel = "LuaMConsoleHazardLabel";
    public const string Docked = "LuaMConsoleDocked";

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

    public static readonly (string Class, Color Color)[] Guns =
    {
        (GunBallistic, HudPalette.Caution),
        (GunEnergy, HudPalette.Advisory),
        (GunMissile, HudPalette.Bad),
    };

    public static StyleRule[] GetRules(IResourceCache resCache)
    {
        var section = HudThemeStyles.Flat(HudPalette.PanelRaised, null);
        section.SetContentMarginOverride(StyleBox.Margin.All, 3);
        var inset = new StyleBoxGrid { Step = 0, BackgroundColor = HudPalette.Background, ScanlineColor = HudPalette.Text.WithAlpha(0.014f), BorderColor = HudPalette.LineDim };
        var header = HudThemeStyles.Flat(Color.Transparent, null);
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

        var thin = LobbyHudStyles.MonoFont(resCache, 11);
        AddThin(rules, Thin, HudPalette.Fg, thin, false);
        AddThin(rules, Toggle, HudPalette.Fg, thin, true);
        foreach (var (cls, color) in Guns)
        {
            AddThin(rules, cls, color, thin, true, Label.AlignMode.Left);
            AddThin(rules, cls + Pick, color, thin, false);
        }

        rules.AddRange(new StyleRule[]
        {
            Element<Label>().Class(Caption)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.UiFont(resCache, 10))
                .Prop(Label.StylePropertyFontColor, HudPalette.TextDim),
            Element<Label>().Class(Key)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.UiFont(resCache, 10))
                .Prop(Label.StylePropertyFontColor, HudPalette.FgLabel),
            Element<Label>().Class(Value)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoMediumFont(resCache, 11))
                .Prop(Label.StylePropertyFontColor, HudPalette.Cream),
            Element<Label>().Class(Readout)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoMediumFont(resCache, 30))
                .Prop(Label.StylePropertyFontColor, HudPalette.Cream),
            Element<Label>().Class(ReadoutUnit)
                .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoFont(resCache, 13))
                .Prop(Label.StylePropertyFontColor, HudPalette.FgLabel),
        });

        rules.Add(Element<PanelContainer>().Class(Hazard).Prop(PanelContainer.StylePropertyPanel, Tape(5, 8, 0.08f)));
        rules.Add(Element<Label>().Class(HazardLabel)
            .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoMediumFont(resCache, 12))
            .Prop(Label.StylePropertyFontColor, HudPalette.Cream));

        foreach (var (pseudo, box) in new[]
                 {
                     (ContainerButton.StylePseudoClassNormal, Tape(4, 6, 0.06f)),
                     (ContainerButton.StylePseudoClassHover, Tape(4, 6, 0.14f)),
                     (ContainerButton.StylePseudoClassPressed, Tape(4, 6, 0.2f, StyleBoxLamp.Mix(HudAccents.Cargo, HudPalette.Background, 0.12f))),
                 })
        {
            rules.Add(Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(Docked).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, box)
                .Prop(Control.StylePropertyModulateSelf, Color.White));
        }

        return rules.ToArray();
    }

    private static StyleBoxHazard Tape(float vertical, float horizontal, float faceStripes, Color? face = null)
    {
        var box = new StyleBoxHazard { Band = 3, Stripe = 6, FaceStripeAlpha = faceStripes, FaceColor = face ?? HudPalette.Background };
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, vertical);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontal);
        return box;
    }

    public static string GunStyle(ShipGunType? type) => type switch
    {
        ShipGunType.Ballistic => GunBallistic,
        ShipGunType.Energy => GunEnergy,
        ShipGunType.Missile => GunMissile,
        _ => Toggle,
    };

    public static HudStatus? GunShape(ShipGunType? type) => type switch
    {
        ShipGunType.Ballistic => HudStatus.Caution,
        ShipGunType.Energy => HudStatus.Advisory,
        ShipGunType.Missile => HudStatus.Warning,
        _ => null,
    };

    public static void AddGunShape(Button button, HudStatus? shape)
    {
        if (shape is not { } status)
            return;

        button.AddChild(new HudShape
        {
            Status = status,
            SetSize = new Vector2(9, 9),
            HorizontalAlignment = Control.HAlignment.Left,
        });
        button.Label.Margin = new Thickness(15, 0, 0, 0);
    }

    public static void Caps(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is Button { Text: { } text } button && button.StyleClasses.Any(c => c.StartsWith("LuaM")))
                button.Text = text.ToUpperInvariant();
            else if (child is Label { Text: { } label } caption && (caption.HasStyleClass(Caption) || caption.HasStyleClass(Key) || caption.HasStyleClass(HeaderLabel) || caption.HasStyleClass(HazardLabel)))
                caption.Text = label.ToUpperInvariant();

            Caps(child);
        }
    }

    private static void AddThin(List<StyleRule> rules, string cls, Color accent, Font font, bool strip, Label.AlignMode? align = null)
    {
        var fg = accent == HudPalette.Fg;
        Color? off = strip ? StyleBoxLamp.Mix(accent, HudPalette.Background, 0.14f) : null;
        var normal = new StyleBoxThin(strip) { Background = HudPalette.Background, Strip = off };
        var hover = new StyleBoxThin(strip) { Background = HudPalette.Background, Border = fg ? HudPalette.BorderBright : accent, Strip = off };
        var pressed = strip
            ? new StyleBoxThin(true)
            {
                Border = fg ? HudPalette.FgDim : StyleBoxLamp.Mix(accent, HudPalette.Border, 0.6f),
                Background = fg ? HudPalette.Background : StyleBoxLamp.Mix(accent, HudPalette.Background, 0.08f),
                Strip = StyleBoxLamp.Mix(accent, HudPalette.Background, 0.8f),
                Glow = true,
            }
            : new StyleBoxThin(false) { Background = StyleBoxLamp.Mix(accent, HudPalette.Background, 0.14f), Border = fg ? HudPalette.FgDim : accent, Sunken = true };
        var disabled = new StyleBoxThin(strip) { Background = HudPalette.Background, Border = HudPalette.LineDim, Strip = strip ? HudPalette.Background : null };

        foreach (var (pseudo, box, label) in new[]
                 {
                     (ContainerButton.StylePseudoClassNormal, normal, HudPalette.Text),
                     (ContainerButton.StylePseudoClassHover, hover, accent),
                     (ContainerButton.StylePseudoClassPressed, pressed, strip && !fg ? HudPalette.Text : HudPalette.Fg),
                     (ContainerButton.StylePseudoClassDisabled, disabled, HudPalette.Offline),
                 })
        {
            rules.Add(Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(cls).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, box)
                .Prop(Control.StylePropertyModulateSelf, Color.White));

            rules.Add(new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(ContainerButton), new[] { cls }, null, new[] { pseudo }),
                    new SelectorElement(typeof(Label), null, null, null)),
                align is { } mode
                    ? new[] { new StyleProperty(Label.StylePropertyFontColor, label), new StyleProperty(Label.StylePropertyFont, font), new StyleProperty(Label.StylePropertyAlignMode, mode) }
                    : new[] { new StyleProperty(Label.StylePropertyFontColor, label), new StyleProperty(Label.StylePropertyFont, font) }));
        }
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
