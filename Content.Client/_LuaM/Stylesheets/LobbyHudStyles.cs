using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets;

public static class LobbyHudStyles
{
    public const string Button = "LuaMHudButton";
    public const string Command = "LuaMHudCommand";
    public const string Primary = "LuaMHudPrimary";
    public const string Danger = "LuaMHudDanger";
    public const string Positive = "LuaMHudPositive";
    public const string Ready = "LuaMHudReady";
    public const string Join = "LuaMHudJoin";
    public const string Priority = "LuaMHudPrio";
    public const string Accent = "LuaMHudAccent";
    public const string Panel = "LuaMHudPanel";
    public const string Window = "LuaMHudWindow";
    public const string Input = "LuaMHudInput";
    public const string Icon = "LuaMHudIcon";
    public const string Power = "LuaMHudPower";

    public const string Title = "LuaMHudTitle";
    public const string Heading = "LuaMHudHeading";
    public const string Big = "LuaMHudBig";
    public const string Mono = "LuaMHudMono";
    public const string MonoDim = "LuaMHudMonoDim";
    public const string Body = "LuaMHudBody";
    public const string BodyBold = "LuaMHudBodyBold";
    public const string Auto = "LuaMHudAuto";
    public const string Option = "LuaMHudOption";
    public const string SliderClass = "LuaMHudSlider";
    public const string ColorSliderClass = "LuaMHudColorSlider";
    public const string List = "LuaMHudList";
    public const string Scroll = "LuaMHudScroll";
    public const string PartId = "LuaMHudPartId";
    public const string Mark = "LuaMHudMark";
    public const string LabelClass = "LuaMHudLabel";
    public const string Data = "LuaMHudData";
    public const string Note = "LuaMHudNote";
    public const string TabTitle = "LuaMHudTab";
    public const string TraitTitle = "LuaMHudTraitTitle";
    public const string NoteLead = "LuaMHudNoteLead";

    private const string Fonts = "/Fonts/_LuaM/";
    private const string Symbols = "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf";

    public static Font UiFont(IResourceCache res, int size, string weight = "SemiBold") =>
        res.GetFont(new[] { Fonts + $"IBMPlexSansCondensed/IBMPlexSansCondensed-{weight}.ttf", "/Fonts/NotoSans/NotoSans-Regular.ttf", Symbols }, size);

    public static Font MonoFont(IResourceCache res, int size, bool semi = false) =>
        res.GetFont(new[] { Fonts + (semi ? "IBMPlexMono/IBMPlexMono-SemiBold.ttf" : "IBMPlexMono/IBMPlexMono-Regular.ttf"), "/Fonts/NotoSans/NotoSans-Regular.ttf", Symbols }, size);

    public static Font MonoMediumFont(IResourceCache res, int size) =>
        res.GetFont(new[] { Fonts + "IBMPlexMono/IBMPlexMono-Medium.ttf", "/Fonts/NotoSans/NotoSans-Regular.ttf", Symbols }, size);

    public static StyleRule[] GetRules(IResourceCache resCache)
    {
        var normal = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.Off);
        var hover = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.Off, HudPalette.BorderBright);
        var pressed = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On);
        var disabled = Lamp(HudPalette.Offline, StyleBoxLamp.Lit.Off, HudPalette.LineDim);

        var legend = StyleBoxLamp.Mix(HudPalette.Fg, HudPalette.Text, 0.45f);

        var primary = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On);
        var accent = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On, HudPalette.Fg);
        accent.InnerGlow = HudPalette.Fg.WithAlpha(0.35f);
        accent.OuterGlow = HudPalette.Fg.WithAlpha(0.07f);
        var accentHover = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On, HudPalette.Cream);
        accentHover.InnerGlow = HudPalette.Fg.WithAlpha(0.5f);
        accentHover.OuterGlow = HudPalette.Fg.WithAlpha(0.1f);
        var primaryHover = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On, HudPalette.Fg);
        var primaryPressed = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On, HudPalette.FgDim);

        var warn = Lamp(HudPalette.Bad, StyleBoxLamp.Lit.On);
        var warnHover = Lamp(HudPalette.Bad, StyleBoxLamp.Lit.On, HudPalette.Bad);
        var notReady = Lamp(HudPalette.Bad, StyleBoxLamp.Lit.Fill);
        var notReadyHover = Lamp(HudPalette.Bad, StyleBoxLamp.Lit.Fill, ring: true);
        var good = Lamp(HudPalette.Good, StyleBoxLamp.Lit.Fill);
        var join = Lamp(HudPalette.Good, StyleBoxLamp.Lit.Fill, ring: true);
        var joinHover = Lamp(Color.FromHex("#8FE093"), StyleBoxLamp.Lit.Fill, ring: true);

        var icon = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.Off, margin: 4);
        var iconHover = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.Off, HudPalette.BorderBright, margin: 4);
        var iconPressed = Lamp(HudPalette.Fg, StyleBoxLamp.Lit.On, margin: 4);
        var iconDisabled = Lamp(HudPalette.Offline, StyleBoxLamp.Lit.Off, HudPalette.LineDim, margin: 4);
        var power = Lamp(HudPalette.Bad, StyleBoxLamp.Lit.On, margin: 4);
        var powerHover = Lamp(HudPalette.Bad, StyleBoxLamp.Lit.On, HudPalette.Bad, margin: 4);

        var panel = StyleBoxHud.Frame();
        var window = StyleBoxHud.Window();

        var input = new StyleBoxFlat
        {
            BackgroundColor = HudPalette.Background,
            BorderColor = HudPalette.LineDim,
            BorderThickness = new Thickness(1),
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5,
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
        };

        var flat = (Color bg, Color border) => new StyleBoxFlat { BackgroundColor = bg, BorderColor = border, BorderThickness = new Thickness(1) };
        var grabber = new StyleBoxFlat { BackgroundColor = HudPalette.Fg };
        grabber.SetContentMarginOverride(StyleBox.Margin.Horizontal, 3);
        grabber.SetContentMarginOverride(StyleBox.Margin.Vertical, 9);
        var colorGrabber = new StyleBoxMarker();
        colorGrabber.SetContentMarginOverride(StyleBox.Margin.Horizontal, 3);
        colorGrabber.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
        var colorFill = new StyleBoxFlat { BackgroundColor = Color.White };
        var scroll = new StyleBoxFlat { BackgroundColor = HudPalette.BorderBright };
        scroll.SetContentMarginOverride(StyleBox.Margin.All, 2);
        var scrollHover = new StyleBoxFlat { BackgroundColor = HudPalette.Fg };
        scrollHover.SetContentMarginOverride(StyleBox.Margin.All, 2);

        var listItem = new StyleBoxFlat { BackgroundColor = Color.Transparent, ContentMarginLeftOverride = 8, ContentMarginTopOverride = 3, ContentMarginBottomOverride = 3 };
        var listSelected = new StyleBoxFlat
        {
            BackgroundColor = HudPalette.FgFaint,
            BorderColor = HudPalette.Fg,
            BorderThickness = new Thickness(2, 0, 0, 0),
            ContentMarginLeftOverride = 8, ContentMarginTopOverride = 3, ContentMarginBottomOverride = 3,
        };

        var ui13 = UiFont(resCache, 13);
        var ui15 = UiFont(resCache, 15);

        return
        [
            Element<Label>().Class(Auto)
                .Prop(Label.StylePropertyFont, UiFont(resCache, 13, "Regular"))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),

            ..ButtonState(Option, ContainerButton.StylePseudoClassNormal, normal),
            ..ButtonState(Option, ContainerButton.StylePseudoClassHover, hover),
            ..ButtonState(Option, ContainerButton.StylePseudoClassPressed, pressed),
            ..ButtonState(Option, ContainerButton.StylePseudoClassDisabled, disabled),
            ..ButtonLabels(Option, UiFont(resCache, 13, "Medium"), legend),

            Element<Slider>().Class(SliderClass)
                .Prop(Slider.StylePropertyBackground, flat(HudPalette.Background, HudPalette.Border))
                .Prop(Slider.StylePropertyForeground, new StyleBoxFlat { BackgroundColor = Color.Transparent, BorderColor = HudPalette.Border, BorderThickness = new Thickness(1) })
                .Prop(Slider.StylePropertyFill, new StyleBoxFlat { BackgroundColor = HudPalette.FgDim })
                .Prop(Slider.StylePropertyGrabber, grabber),

            Element<ColorableSlider>().Class(ColorSliderClass)
                .Prop(Slider.StylePropertyForeground, new StyleBoxFlat { BackgroundColor = Color.Transparent, BorderColor = HudPalette.Border, BorderThickness = new Thickness(1) })
                .Prop(Slider.StylePropertyGrabber, colorGrabber)
                .Prop(ColorableSlider.StylePropertyFillWhite, colorFill)
                .Prop(ColorableSlider.StylePropertyBackgroundWhite, colorFill),

            Element<ItemList>().Class(List)
                .Prop(ItemList.StylePropertyBackground, new StyleBoxGrid { Step = 0 })
                .Prop(ItemList.StylePropertyItemBackground, listItem)
                .Prop(ItemList.StylePropertySelectedItemBackground, listSelected)
                .Prop(ItemList.StylePropertyDisabledItemBackground, listItem),

            Element<ScrollBar>().Class(Scroll)
                .Prop(ScrollBar.StylePropertyGrabber, scroll),
            Element<ScrollBar>().Class(Scroll).Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, scrollHover),
            Element<ScrollBar>().Class(Scroll).Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, scrollHover),

            Element<PanelContainer>().Class(Panel)
                .Prop(PanelContainer.StylePropertyPanel, panel),
            Element<PanelContainer>().Class(Window)
                .Prop(PanelContainer.StylePropertyPanel, window),
            ..StatusWindows(),

            Element<LineEdit>().Class(Input)
                .Prop(LineEdit.StylePropertyStyleBox, input)
                .Prop("font", UiFont(resCache, 13, "Medium")),

            Element<Label>().Class(Title)
                .Prop(Label.StylePropertyFont, UiFont(resCache, 24))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<Label>().Class(Heading)
                .Prop(Label.StylePropertyFont, MonoMediumFont(resCache, 12))
                .Prop(Label.StylePropertyFontColor, HudPalette.Advisory),
            Element<Label>().Class(TabTitle)
                .Prop(Label.StylePropertyFont, UiFont(resCache, 13))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<Label>().Class(PartId)
                .Prop(Label.StylePropertyFont, MonoMediumFont(resCache, 11))
                .Prop(Label.StylePropertyFontColor, HudPalette.Fg),
            Element<Label>().Class(Mark)
                .Prop(Label.StylePropertyFont, MonoFont(resCache, 10))
                .Prop(Label.StylePropertyFontColor, HudPalette.TextDim),
            Element<Label>().Class(LabelClass)
                .Prop(Label.StylePropertyFont, UiFont(resCache, 11, "Medium"))
                .Prop(Label.StylePropertyFontColor, HudPalette.Fg),
            Element<Label>().Class(TraitTitle)
                .Prop(Label.StylePropertyFont, UiFont(resCache, 18))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<Label>().Class(Note)
                .Prop(Label.StylePropertyFont, MonoFont(resCache, 11))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<Label>().Class(NoteLead)
                .Prop(Label.StylePropertyFont, MonoMediumFont(resCache, 11))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<Label>().Class(Data)
                .Prop(Label.StylePropertyFont, MonoFont(resCache, 15))
                .Prop(Label.StylePropertyFontColor, HudPalette.Fg),
            Element<Label>().Class("LuaMHudBigBold")
                .Prop(Label.StylePropertyFont, MonoFont(resCache, 48, true))
                .Prop(Label.StylePropertyFontColor, HudPalette.Advisory),
            Element<Label>().Class(Big)
                .Prop(Label.StylePropertyFont, MonoMediumFont(resCache, 48))
                .Prop(Label.StylePropertyFontColor, HudPalette.Fg),
            Element<Label>().Class(Mono)
                .Prop(Label.StylePropertyFont, MonoFont(resCache, 11))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<Label>().Class(MonoDim)
                .Prop(Label.StylePropertyFont, MonoFont(resCache, 11))
                .Prop(Label.StylePropertyFontColor, HudPalette.TextDim),
            Element<Label>().Class(Body)
                .Prop(Label.StylePropertyFont, UiFont(resCache, 13, "Regular"))
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),
            Element<RichTextLabel>().Class(Body)
                .Prop("font", UiFont(resCache, 13, "Regular")),
            Element<Label>().Class(BodyBold)
                .Prop(Label.StylePropertyFont, ui15)
                .Prop(Label.StylePropertyFontColor, HudPalette.Text),

            Element<ContainerButton>().Class(Button)
                .Prop(ContainerButton.StylePropertyStyleBox, normal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            ..States(Button, normal, hover, pressed, disabled),
            ..States(Command, normal, hover, pressed, disabled),
            ..States(Icon, icon, iconHover, iconPressed, iconDisabled),
            ..States(Primary, primary, primaryHover, primaryPressed, disabled),
            ..States(Accent, accent, accentHover, primaryPressed, disabled),
            ..States(Danger, warn, warnHover, warnHover, disabled),
            ..States(Positive, Lamp(HudPalette.Good, StyleBoxLamp.Lit.On), Lamp(HudPalette.Good, StyleBoxLamp.Lit.On, HudPalette.Good), Lamp(HudPalette.Good, StyleBoxLamp.Lit.Fill), disabled),
            ..States(Power, power, powerHover, powerHover, iconDisabled),
            ..States(Ready, notReady, notReadyHover, good, disabled),
            ..States(Join, join, joinHover, joinHover, disabled),

            ..PriorityRules(ui13),

            ..ButtonLabels(Button, ui13, legend),
            ButtonLabel(Button, ContainerButton.StylePseudoClassHover, null, HudPalette.Fg),
            ButtonLabel(Button, ContainerButton.StylePseudoClassPressed, null, HudPalette.Fg),
            ButtonLabel(Button, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ..ButtonLabels(Command, ui13, legend),
            ButtonLabel(Command, ContainerButton.StylePseudoClassHover, null, HudPalette.Fg),
            ButtonLabel(Command, ContainerButton.StylePseudoClassPressed, null, HudPalette.Fg),
            ButtonLabel(Command, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ..ButtonLabels(Primary, ui15, HudPalette.Fg),
            ButtonLabel(Primary, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ..ButtonLabels(Accent, ui13, HudPalette.Fg),
            ButtonLabel(Accent, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ..ButtonLabels(Danger, ui13, HudPalette.Bad),
            ButtonLabel(Danger, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ..ButtonLabels(Positive, ui13, HudPalette.Good),
            ButtonLabel(Positive, ContainerButton.StylePseudoClassPressed, null, HudPalette.Void),
            ButtonLabel(Positive, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ..ButtonLabels(Ready, UiFont(resCache, 20), HudPalette.Void),
            ..ButtonLabels(Join, UiFont(resCache, 20), HudPalette.Void),
            ButtonLabel(Join, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
            ButtonLabel(Ready, ContainerButton.StylePseudoClassPressed, null, HudPalette.Void),
            ButtonLabel(Ready, ContainerButton.StylePseudoClassDisabled, null, HudPalette.Offline),
        ];
    }

    public static string WindowFor(HudStatus status) => Window + status;

    private static IEnumerable<StyleRule> StatusWindows()
    {
        foreach (var status in new[] { HudStatus.Nominal, HudStatus.Advisory, HudStatus.Caution, HudStatus.Warning })
        {
            var color = HudStatusColors.Of(status);
            var box = StyleBoxHud.Window(null, color);
            box.HeaderColor = StyleBoxLamp.Mix(color, HudPalette.PanelRaised, 0.1f);
            box.InnerColor = StyleBoxLamp.Mix(color, HudPalette.LineDim, 0.4f);
            yield return Element<PanelContainer>().Class(WindowFor(status))
                .Prop(PanelContainer.StylePropertyPanel, box);
        }
    }

    private static readonly Color[] PriorityColors = { HudPalette.Advisory, HudPalette.Good, HudPalette.Caution, HudPalette.Bad };

    private static IEnumerable<StyleRule> PriorityRules(Font font)
    {
        for (var i = 0; i < PriorityColors.Length; i++)
        {
            var color = PriorityColors[i];
            var fill = i >= 2;
            var cls = Priority + i;
            var lit = Lamp(color, fill ? StyleBoxLamp.Lit.Fill : StyleBoxLamp.Lit.On);
            foreach (var rule in States(cls,
                         Lamp(color, StyleBoxLamp.Lit.Off),
                         Lamp(color, StyleBoxLamp.Lit.Off, color),
                         lit,
                         Lamp(HudPalette.Offline, StyleBoxLamp.Lit.Off, HudPalette.LineDim)))
            {
                yield return rule;
            }

            foreach (var rule in ButtonLabels(cls, font, StyleBoxLamp.Mix(color, HudPalette.Background, 0.55f)))
                yield return rule;
            yield return ButtonLabel(cls, ContainerButton.StylePseudoClassHover, null, color);
            yield return ButtonLabel(cls, ContainerButton.StylePseudoClassPressed, null, fill ? HudPalette.Void : color);
        }
    }

    private static StyleBoxLamp Lamp(Color lamp, StyleBoxLamp.Lit lit, Color? caseColor = null, bool ring = false, float? margin = null)
    {
        var box = new StyleBoxLamp(lamp, lit, caseColor, ring);
        if (margin is { } m)
            box.SetContentMarginOverride(StyleBox.Margin.All, m);
        return box;
    }

    private static StyleRule[] States(string cls, StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox disabled)
    {
        return
        [
            ..ButtonState(cls, ContainerButton.StylePseudoClassNormal, normal),
            ..ButtonState(cls, ContainerButton.StylePseudoClassHover, hover),
            ..ButtonState(cls, ContainerButton.StylePseudoClassPressed, pressed),
            ..ButtonState(cls, ContainerButton.StylePseudoClassDisabled, disabled),
        ];
    }

    private static IEnumerable<StyleRule> ButtonLabels(string buttonClass, Font? font, Color color)
    {
        yield return ButtonLabel(buttonClass, null, font, color);
        yield return ButtonLabel(buttonClass, ContainerButton.StylePseudoClassNormal, font, color);
        yield return ButtonLabel(buttonClass, ContainerButton.StylePseudoClassHover, font, color);
        yield return ButtonLabel(buttonClass, ContainerButton.StylePseudoClassPressed, font, color);
        yield return ButtonLabel(buttonClass, ContainerButton.StylePseudoClassDisabled, font, color);
    }

    private static StyleRule ButtonLabel(string buttonClass, string? pseudo, Font? font, Color color)
    {
        var props = new List<StyleProperty> { new(Label.StylePropertyFontColor, color) };
        if (font != null)
            props.Add(new StyleProperty(Label.StylePropertyFont, font));

        return new StyleRule(new SelectorChild(
                new SelectorElement(typeof(ContainerButton), new[] { buttonClass }, null, pseudo == null ? null : new[] { pseudo }),
                new SelectorElement(typeof(Label), null, null, null)),
            props);
    }

    private static StyleRule[] ButtonState(string buttonClass, string pseudoClass, StyleBox styleBox)
    {
        return
        [
            Element<ContainerButton>().Class(buttonClass).Pseudo(pseudoClass)
                .Prop(ContainerButton.StylePropertyStyleBox, styleBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(buttonClass).Pseudo(pseudoClass)
                .Prop(ContainerButton.StylePropertyStyleBox, styleBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
        ];
    }
}
