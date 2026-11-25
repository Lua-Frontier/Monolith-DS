using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.ContextMenu.UI;
using Content.Client.Examine;
using Content.Client.PDA;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets;

public static class HudThemeStyles
{
    public const string WindowTitleClass = "LuaMHudWindowTitle";
    public const string ListRow = "LuaMHudRow";

    private static readonly string[] Pseudos =
    {
        ContainerButton.StylePseudoClassNormal,
        ContainerButton.StylePseudoClassHover,
        ContainerButton.StylePseudoClassPressed,
        ContainerButton.StylePseudoClassDisabled,
    };

    public static StyleRule[] GetRules(IResourceCache res)
    {
        var rules = new List<StyleRule>();

        var body = LobbyHudStyles.UiFont(res, 12, "Regular");
        var legendFont = LobbyHudStyles.UiFont(res, 12, "Medium");
        var title = LobbyHudStyles.MonoMediumFont(res, 12);
        var legend = StyleBoxLamp.Mix(HudPalette.Fg, HudPalette.Text, 0.45f);

        rules.Add(Element<Label>()
            .Prop(Label.StylePropertyFont, body)
            .Prop(Label.StylePropertyFontColor, HudPalette.Text));

        AddButtons(rules, ContainerButton.StyleClassButton, HudPalette.Fg, legend, legendFont, true);
        AddButtons(rules, StyleBase.ButtonCaution, HudPalette.Caution, HudPalette.Caution, legendFont, true);
        AddButtons(rules, "ButtonColorRed", HudPalette.Bad, HudPalette.Bad, legendFont, false);
        AddButtons(rules, "Danger", HudPalette.Bad, HudPalette.Bad, legendFont, false);
        AddButtons(rules, "ButtonColorGreen", HudPalette.Good, HudPalette.Good, legendFont, false);
        AddButtons(rules, "ButtonAccept", HudPalette.Good, HudPalette.Good, legendFont, false);
        AddButtons(rules, StyleBase.ButtonOpenRightSelected, HudPalette.Fg, HudPalette.Fg, legendFont, false, true);
        AddButtons(rules, StyleBase.ButtonOpenBothSelected, HudPalette.Fg, HudPalette.Fg, legendFont, false, true);

        AddConfirm(rules, null, HudPalette.Caution);
        AddConfirm(rules, "LuaMConfirmGood", HudPalette.Good);
        AddConfirm(rules, "LuaMConfirmBad", HudPalette.Bad);

        foreach (var pseudo in Pseudos)
        {
            rules.Add(new StyleRule(new SelectorElement(typeof(OptionButton), null, null, new[] { pseudo }), new[]
            {
                new StyleProperty(ContainerButton.StylePropertyStyleBox, ButtonBox(HudPalette.Fg, pseudo)),
                new StyleProperty(Control.StylePropertyModulateSelf, Color.White),
            }));
        }

        rules.Add(Element<TextureRect>().Class(OptionButton.StyleClassOptionTriangle)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.Fg));
        rules.Add(Element<Label>().Class(OptionButton.StyleClassOptionButton)
            .Prop(Label.StylePropertyFont, legendFont)
            .Prop(Label.StylePropertyFontColor, HudPalette.Text));
        rules.Add(Element<PanelContainer>().Class(OptionButton.StyleClassOptionsBackground)
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Panel, HudPalette.Border)));

        AddHud(rules, res);
        AddWindows(rules, res, title);
        AddPanels(rules);
        AddInputs(rules, res);
        AddLabels(rules, res);

        return rules.ToArray();
    }

    private static void AddConfirm(List<StyleRule> rules, string? cls, Color color)
    {
        foreach (var pseudo in Pseudos)
        {
            var box = pseudo switch
            {
                ContainerButton.StylePseudoClassPressed => Lamp(color, StyleBoxLamp.Lit.Fill),
                ContainerButton.StylePseudoClassHover => Lamp(color, StyleBoxLamp.Lit.Fill, color),
                ContainerButton.StylePseudoClassDisabled => Lamp(color, StyleBoxLamp.Lit.On, StyleBoxLamp.Mix(color, HudPalette.Border, 0.5f)),
                _ => Lamp(color, StyleBoxLamp.Lit.Fill),
            };
            var selector = Element<ConfirmButton>();
            if (cls != null)
                selector = selector.Class(cls);
            rules.Add(selector.Pseudo(ConfirmButton.ConfirmPrefix + pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, box)
                .Prop(Control.StylePropertyModulateSelf, Color.White));
            rules.Add(new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(ConfirmButton), cls == null ? null : new[] { cls }, null, new[] { ConfirmButton.ConfirmPrefix + pseudo }),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFontColor, pseudo == ContainerButton.StylePseudoClassDisabled ? color : HudPalette.Void) }));
        }

        if (cls == null)
            return;

        foreach (var pseudo in Pseudos)
        {
            var box = ButtonBox(color, pseudo);
            rules.Add(Element<ConfirmButton>().Class(cls).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, box)
                .Prop(Control.StylePropertyModulateSelf, Color.White));
            rules.Add(new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(ConfirmButton), new[] { cls }, null, new[] { pseudo }),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFontColor, pseudo == ContainerButton.StylePseudoClassDisabled ? HudPalette.Offline : StyleBoxLamp.Mix(color, HudPalette.Text, 0.6f)) }));
        }
    }

    private static void AddWindows(List<StyleRule> rules, IResourceCache res, Font title)
    {
        rules.Add(Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel)
            .Prop(PanelContainer.StylePropertyPanel, WindowFrame(null)));
        rules.Add(Element<PanelContainer>().Class(StyleBase.ClassAngleRect)
            .Prop(PanelContainer.StylePropertyPanel, WindowFrame(null))
            .Prop(Control.StylePropertyModulateSelf, Color.White));

        rules.Add(Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowHeader)
            .Prop(PanelContainer.StylePropertyPanel, Header(null, true)));
        rules.Add(Element<PanelContainer>().Class("windowHeaderAlert")
            .Prop(PanelContainer.StylePropertyPanel, Header(HudPalette.Bad, true)));
        rules.Add(Element<PanelContainer>().Class("WindowHeadingBackground")
            .Prop(PanelContainer.StylePropertyPanel, Header(null, false))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("WindowHeadingBackgroundLight")
            .Prop(PanelContainer.StylePropertyPanel, Header(null, false))
            .Prop(Control.StylePropertyModulateSelf, Color.White));

        rules.Add(Element<Label>().Class("FancyWindowTitle")
            .Prop(Label.StylePropertyFont, LobbyHudStyles.UiFont(res, 13))
            .Prop(Label.StylePropertyFontColor, HudPalette.Fg));
        foreach (var cls in new[] { DefaultWindow.StyleClassWindowTitle, WindowTitleClass })
        {
            rules.Add(Element<Label>().Class(cls)
                .Prop(Label.StylePropertyFont, title)
                .Prop(Label.StylePropertyFontColor, HudPalette.Fg));
        }
        rules.Add(Element<Label>().Class("FancyWindowTitle").Class(WindowTitleClass)
            .Prop(Label.StylePropertyFont, title));

        rules.Add(Element<Label>().Class("windowTitleAlert")
            .Prop(Label.StylePropertyFont, title)
            .Prop(Label.StylePropertyFontColor, HudPalette.Bad));

        rules.Add(Element<TextureButton>().Class(DefaultWindow.StyleClassWindowCloseButton)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.TextDim));
        rules.Add(Element<TextureButton>().Class(DefaultWindow.StyleClassWindowCloseButton).Pseudo(TextureButton.StylePseudoClassHover)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.Bad));
        rules.Add(Element<TextureButton>().Class(DefaultWindow.StyleClassWindowCloseButton).Pseudo(TextureButton.StylePseudoClassPressed)
            .Prop(Control.StylePropertyModulateSelf, StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Background, 0.7f)));
        rules.Add(Element<TextureButton>().Class(FancyWindow.StyleClassWindowHelpButton)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.TextDim));
        rules.Add(Element<TextureButton>().Class(FancyWindow.StyleClassWindowHelpButton).Pseudo(TextureButton.StylePseudoClassHover)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.Advisory));

        foreach (var accent in HudAccents.Tinted)
        {
            var color = HudAccents.Of(accent);
            var cls = HudAccents.Class(accent);
            var frame = WindowFrame(color);
            var header = Header(color, true);
            var fancyHeader = Header(color, false);

            rules.Add(Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel).Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, frame));
            rules.Add(Element<PanelContainer>().Class(StyleBase.ClassAngleRect).Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, frame));
            rules.Add(Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowHeader).Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, header));
            rules.Add(Element<PanelContainer>().Class("WindowHeadingBackground").Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, fancyHeader));
            rules.Add(Element<PanelContainer>().Class("WindowHeadingBackgroundLight").Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, fancyHeader));
            rules.Add(Element<Label>().Class(cls)
                .Prop(Label.StylePropertyFontColor, color));
            rules.Add(Element<HudSquare>().Class(cls)
                .Prop(HudSquare.StylePropertyColor, color));
        }

        rules.Add(Element<HudSquare>()
            .Prop(HudSquare.StylePropertyColor, HudPalette.Fg));

        foreach (var accent in HudAccents.Tinted)
        {
            var color = HudAccents.Of(accent);
            var cls = HudAccents.Class(accent);
            var shell = WindowFrame(color);
            shell.SetContentMarginOverride(StyleBox.Margin.All, 10);
            var header = Header(color, true);
            header.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
            rules.Add(Element<PanelContainer>().Class(DeadSpaceAHelpStyles.Shell).Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, shell));
            rules.Add(Element<PanelContainer>().Class(DeadSpaceAHelpStyles.Header).Class(cls)
                .Prop(PanelContainer.StylePropertyPanel, header));
            rules.Add(Element<Label>().Class(DeadSpaceAHelpStyles.Title).Class(cls)
                .Prop(Label.StylePropertyFontColor, color));
        }

        rules.Add(new StyleRule(SelectorElement.Type(typeof(StripeBack)), new[]
        {
            new StyleProperty(StripeBack.StylePropertyBackground, new StyleBoxGrid { Step = 0 }),
        }));

        var nanoHeading = new StyleBoxHudHeader { Inset = 0, Chamfer = 0, AccentColor = HudPalette.Advisory };
        nanoHeading.SetContentMarginOverride(StyleBox.Margin.Left, 10);
        nanoHeading.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        rules.Add(new StyleRule(new SelectorChild(
                SelectorElement.Type(typeof(NanoHeading)),
                SelectorElement.Type(typeof(PanelContainer))),
            new[]
            {
                new StyleProperty(PanelContainer.StylePropertyPanel, nanoHeading),
            }));
    }

    private static void AddHud(List<StyleRule> rules, IResourceCache res)
    {
        foreach (var pseudo in Pseudos)
        {
            var box = ButtonBox(HudPalette.Fg, pseudo);
            box.SetContentMarginOverride(StyleBox.Margin.All, 0);
            rules.Add(new StyleRule(new SelectorElement(typeof(MenuButton), null, null, new[] { pseudo }), new[]
            {
                new StyleProperty(Button.StylePropertyStyleBox, box),
                new StyleProperty(Control.StylePropertyModulateSelf, Color.White),
            }));
            foreach (var shape in new[] { StyleBase.ButtonSquare, StyleBase.ButtonOpenLeft, StyleBase.ButtonOpenRight })
            {
                rules.Add(new StyleRule(new SelectorElement(typeof(MenuButton), new[] { shape }, null, new[] { pseudo }), new[]
                {
                    new StyleProperty(Button.StylePropertyStyleBox, box),
                    new StyleProperty(Control.StylePropertyModulateSelf, Color.White),
                }));
            }

            var red = pseudo == ContainerButton.StylePseudoClassDisabled
                ? box
                : (StyleBox) Lamp(HudPalette.Bad, pseudo == ContainerButton.StylePseudoClassNormal ? StyleBoxLamp.Lit.Off : StyleBoxLamp.Lit.On, StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Border, 0.5f));
            red.SetContentMarginOverride(StyleBox.Margin.All, 0);
            rules.Add(new StyleRule(new SelectorElement(typeof(MenuButton), new[] { MenuButton.StyleClassRedTopButton }, null, new[] { pseudo }), new[]
            {
                new StyleProperty(Button.StylePropertyStyleBox, red),
                new StyleProperty(Control.StylePropertyModulateSelf, Color.White),
            }));
        }

        rules.Add(Element<Label>().Class(MenuButton.StyleClassLabelTopButton)
            .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoMediumFont(res, 12))
            .Prop(Label.StylePropertyFontColor, HudPalette.FgLabel));

        var chat = new StyleBoxHud(HudPalette.Panel.WithAlpha(0.82f), HudPalette.Border);
        chat.SetContentMarginOverride(StyleBox.Margin.All, 2);
        var chatSub = new StyleBoxHud(HudPalette.Panel.WithAlpha(0.82f), HudPalette.Border);
        chatSub.SetContentMarginOverride(StyleBox.Margin.All, 2);
        rules.Add(Element<PanelContainer>().Class(StyleNano.StyleClassChatPanel)
            .Prop(PanelContainer.StylePropertyPanel, chat));
        rules.Add(Element<PanelContainer>().Class(StyleNano.StyleClassChatSubPanel)
            .Prop(PanelContainer.StylePropertyPanel, chatSub));

        foreach (var pseudo in Pseudos)
        {
            var box = ButtonBox(HudPalette.Fg, pseudo);
            box.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);
            rules.Add(new StyleRule(new SelectorElement(typeof(Button), new[] { StyleNano.StyleClassChatChannelSelectorButton }, null, new[] { pseudo }), new[]
            {
                new StyleProperty(Button.StylePropertyStyleBox, box),
                new StyleProperty(Control.StylePropertyModulateSelf, Color.White),
            }));
            rules.Add(new StyleRule(new SelectorElement(typeof(ContainerButton), new[] { StyleNano.StyleClassChatFilterOptionButton }, null, new[] { pseudo }), new[]
            {
                new StyleProperty(ContainerButton.StylePropertyStyleBox, box),
                new StyleProperty(Control.StylePropertyModulateSelf, Color.White),
            }));

            var storage = ButtonBox(HudPalette.Fg, pseudo);
            storage.SetContentMarginOverride(StyleBox.Margin.Vertical, 0);
            storage.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
            rules.Add(Element<ContainerButton>().Class(StyleNano.StyleClassStorageButton).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, storage)
                .Prop(Control.StylePropertyModulateSelf, Color.White));

            var row = pseudo switch
            {
                ContainerButton.StylePseudoClassHover => new StyleBoxFlat { BackgroundColor = HudPalette.PanelHover, BorderColor = HudPalette.FgDim, BorderThickness = new Thickness(2, 0, 0, 0) },
                ContainerButton.StylePseudoClassPressed => new StyleBoxFlat { BackgroundColor = HudPalette.FgFaint, BorderColor = HudPalette.Fg, BorderThickness = new Thickness(2, 0, 0, 0) },
                ContainerButton.StylePseudoClassDisabled => new StyleBoxFlat { BackgroundColor = HudPalette.Background },
                _ => new StyleBoxFlat { BackgroundColor = HudPalette.PanelRaised },
            };
            row.ContentMarginLeftOverride = 10;
            rules.Add(Element<ContainerButton>().Class(ListContainer.StyleClassListContainerButton).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, row)
                .Prop(Control.StylePropertyModulateSelf, Color.White));

            var context = pseudo switch
            {
                ContainerButton.StylePseudoClassHover => new StyleBoxFlat { BackgroundColor = HudPalette.FgFaint, BorderColor = HudPalette.Fg, BorderThickness = new Thickness(2, 0, 0, 0) },
                ContainerButton.StylePseudoClassPressed => new StyleBoxFlat { BackgroundColor = HudPalette.FgDim },
                ContainerButton.StylePseudoClassDisabled => new StyleBoxFlat { BackgroundColor = HudPalette.Background },
                _ => new StyleBoxFlat { BackgroundColor = HudPalette.Panel },
            };
            rules.Add(Element<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, context)
                .Prop(Control.StylePropertyModulateSelf, Color.White));
            rules.Add(Element<ExamineButton>().Class(ExamineButton.StyleClassExamineButton).Pseudo(pseudo)
                .Prop(Control.StylePropertyModulateSelf, pseudo == ContainerButton.StylePseudoClassNormal ? HudPalette.FgDim : HudPalette.Fg));
        }

        var pdaFrame = WindowFrame(HudPalette.Fg);
        pdaFrame.BackgroundColor = HudPalette.Void;
        rules.Add(Element<PanelContainer>().Class("PdaBackgroundRect")
            .Prop(PanelContainer.StylePropertyPanel, pdaFrame)
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("PdaBorderRect")
            .Prop(PanelContainer.StylePropertyPanel, new StyleBoxEmpty()));
        rules.Add(Element<PanelContainer>().Class("PdaBackground")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Background, HudPalette.LineDim))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("PdaContentBackground")
            .Prop(PanelContainer.StylePropertyPanel, new StyleBoxGrid { Step = 0 })
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<Label>().Class("PdaContentFooterText")
            .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoFont(res, 10))
            .Prop(Label.StylePropertyFontColor, HudPalette.TextDim));
        rules.Add(Element<Label>().Class("PdaWindowFooterText")
            .Prop(Label.StylePropertyFont, LobbyHudStyles.MonoFont(res, 10))
            .Prop(Label.StylePropertyFontColor, HudPalette.FgLabel));
        rules.Add(Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassNormal)
            .Prop(PdaSettingsButton.StylePropertyBgColor, HudPalette.PanelRaised)
            .Prop(PdaSettingsButton.StylePropertyFgColor, HudPalette.Text));
        rules.Add(Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassHover)
            .Prop(PdaSettingsButton.StylePropertyBgColor, HudPalette.FgFaint)
            .Prop(PdaSettingsButton.StylePropertyFgColor, HudPalette.Fg));
        rules.Add(Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassPressed)
            .Prop(PdaSettingsButton.StylePropertyBgColor, HudPalette.FgDim)
            .Prop(PdaSettingsButton.StylePropertyFgColor, HudPalette.Cream));
        rules.Add(Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassDisabled)
            .Prop(PdaSettingsButton.StylePropertyBgColor, HudPalette.Panel)
            .Prop(PdaSettingsButton.StylePropertyFgColor, HudPalette.Offline));
        rules.Add(Element<PdaProgramItem>().Pseudo(ContainerButton.StylePseudoClassNormal)
            .Prop(PdaProgramItem.StylePropertyBgColor, HudPalette.PanelRaised));
        rules.Add(Element<PdaProgramItem>().Pseudo(ContainerButton.StylePseudoClassHover)
            .Prop(PdaProgramItem.StylePropertyBgColor, HudPalette.FgFaint));
        rules.Add(Element<PdaProgramItem>().Pseudo(ContainerButton.StylePseudoClassPressed)
            .Prop(PdaProgramItem.StylePropertyBgColor, HudPalette.FgDim));

        var listFont = res.NotoStack("Regular", 12);
        foreach (var pseudo in Pseudos)
        {
            var row = pseudo switch
            {
                ContainerButton.StylePseudoClassHover => new StyleBoxFlat { BackgroundColor = HudPalette.PanelHover, BorderColor = HudPalette.FgDim, BorderThickness = new Thickness(2, 0, 0, 0) },
                ContainerButton.StylePseudoClassPressed => new StyleBoxFlat { BackgroundColor = HudPalette.FgFaint, BorderColor = HudPalette.Fg, BorderThickness = new Thickness(2, 0, 0, 0) },
                ContainerButton.StylePseudoClassDisabled => new StyleBoxFlat { BackgroundColor = HudPalette.Background },
                _ => new StyleBoxFlat { BackgroundColor = HudPalette.Panel, BorderColor = HudPalette.LineDim, BorderThickness = new Thickness(0, 0, 0, 1) },
            };
            row.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
            row.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
            rules.Add(Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(ListRow).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, row)
                .Prop(Control.StylePropertyModulateSelf, Color.White));
            rules.Add(new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(ContainerButton), new[] { ListRow }, null, new[] { pseudo }),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[]
                {
                    new StyleProperty(Label.StylePropertyFont, listFont),
                    new StyleProperty(Label.StylePropertyFontColor, pseudo switch
                    {
                        ContainerButton.StylePseudoClassDisabled => HudPalette.Offline,
                        ContainerButton.StylePseudoClassHover or ContainerButton.StylePseudoClassPressed => HudPalette.Cream,
                        _ => HudPalette.Text,
                    }),
                }));
        }

        rules.Add(Element<PanelContainer>().Class(ContextMenuPopup.StyleClassContextMenuPopup)
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Panel.WithAlpha(0.97f), HudPalette.Border)));
        rules.Add(Element<LineEdit>().Class(StyleNano.StyleClassActionSearchBox)
            .Prop(LineEdit.StylePropertyStyleBox, Flat(HudPalette.Background, HudPalette.Border)));
    }

    private static void AddPanels(List<StyleRule> rules)
    {
        var bordered = StyleBoxHud.Frame(null, null, 2);
        bordered.Chamfer = 0;
        bordered.Double = false;
        var transparent = StyleBoxHud.Frame(HudPalette.Panel.WithAlpha(0.8f), null, 2);
        transparent.Chamfer = 0;
        transparent.Double = false;

        rules.Add(Element<PanelContainer>().Class(StyleNano.StyleClassBorderedWindowPanel)
            .Prop(PanelContainer.StylePropertyPanel, bordered));
        rules.Add(Element<PanelContainer>().Class(StyleNano.StyleClassTransparentBorderedWindowPanel)
            .Prop(PanelContainer.StylePropertyPanel, transparent));

        rules.Add(Element<PanelContainer>().Class("PanelBackgroundBaseDark")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Background, HudPalette.LineDim))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("PanelBackgroundBaseLight")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.PanelRaised, HudPalette.LineDim))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("PanelBackgroundLight")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.PanelHover, HudPalette.Border))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("BackgroundDark")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Background, null))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("BackgroundOpenRight")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Panel, HudPalette.LineDim))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class("BackgroundOpenLeft")
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Panel, HudPalette.LineDim))
            .Prop(Control.StylePropertyModulateSelf, Color.White));
        rules.Add(Element<PanelContainer>().Class(StyleNano.StyleClassInset)
            .Prop(PanelContainer.StylePropertyPanel, Flat(HudPalette.Background, HudPalette.LineDim)));

        rules.Add(Element<PanelContainer>().Class(StyleBase.ClassLowDivider)
            .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat
            {
                BackgroundColor = HudPalette.LineDim,
                ContentMarginLeftOverride = 1,
                ContentMarginBottomOverride = 1,
            }));
        rules.Add(Element<PanelContainer>().Class(StyleBase.ClassHighDivider)
            .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat
            {
                BackgroundColor = HudPalette.FgDim,
                ContentMarginLeftOverride = 1,
                ContentMarginBottomOverride = 1,
            }));

        var tooltip = Flat(HudPalette.Panel.WithAlpha(0.97f), HudPalette.Border);
        tooltip.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);
        tooltip.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        rules.Add(new StyleRule(new SelectorElement(typeof(Tooltip), null, null, null), new[]
        {
            new StyleProperty(PanelContainer.StylePropertyPanel, tooltip),
        }));
        rules.Add(Element<PanelContainer>().Class(StyleNano.StyleClassTooltipPanel)
            .Prop(PanelContainer.StylePropertyPanel, tooltip));

        var tabPanel = Flat(HudPalette.Panel, HudPalette.LineDim);
        tabPanel.SetContentMarginOverride(StyleBox.Margin.All, 3);
        var tabActive = new StyleBoxFlat
        {
            BackgroundColor = HudPalette.FgFaint,
            BorderColor = HudPalette.Fg,
            BorderThickness = new Thickness(0, 0, 0, 2),
        };
        tabActive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        tabActive.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        var tabInactive = new StyleBoxFlat
        {
            BackgroundColor = HudPalette.PanelRaised,
            BorderColor = HudPalette.LineDim,
            BorderThickness = new Thickness(0, 0, 0, 2),
        };
        tabInactive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        tabInactive.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        rules.Add(new StyleRule(new SelectorElement(typeof(TabContainer), null, null, null), new[]
        {
            new StyleProperty(TabContainer.StylePropertyPanelStyleBox, tabPanel),
            new StyleProperty(TabContainer.StylePropertyTabStyleBox, tabActive),
            new StyleProperty(TabContainer.StylePropertyTabStyleBoxInactive, tabInactive),
            new StyleProperty(TabContainer.stylePropertyTabFontColor, HudPalette.Fg),
            new StyleProperty(TabContainer.StylePropertyTabFontColorInactive, HudPalette.TextDim),
        }));

        var item = new StyleBoxFlat { BackgroundColor = HudPalette.PanelRaised };
        item.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        item.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
        var itemSelected = new StyleBoxFlat
        {
            BackgroundColor = HudPalette.FgFaint,
            BorderColor = HudPalette.Fg,
            BorderThickness = new Thickness(2, 0, 0, 0),
        };
        itemSelected.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        itemSelected.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
        var itemDisabled = new StyleBoxFlat { BackgroundColor = HudPalette.Background };
        itemDisabled.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        itemDisabled.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
        rules.Add(new StyleRule(new SelectorElement(typeof(ItemList), null, null, null), new[]
        {
            new StyleProperty(ItemList.StylePropertyBackground, Flat(HudPalette.Background, HudPalette.LineDim)),
            new StyleProperty(ItemList.StylePropertyItemBackground, item),
            new StyleProperty(ItemList.StylePropertySelectedItemBackground, itemSelected),
            new StyleProperty(ItemList.StylePropertyDisabledItemBackground, itemDisabled),
        }));
        rules.Add(new StyleRule(new SelectorElement(typeof(ItemList), new[] { "transparentItemList" }, null, null), new[]
        {
            new StyleProperty(ItemList.StylePropertySelectedItemBackground, itemSelected),
        }));
        rules.Add(new StyleRule(new SelectorElement(typeof(ItemList), new[] { "transparentBackgroundItemList" }, null, null), new[]
        {
            new StyleProperty(ItemList.StylePropertySelectedItemBackground, itemSelected),
        }));

        rules.Add(new StyleRule(new SelectorElement(typeof(Tree), null, null, null), new[]
        {
            new StyleProperty(Tree.StylePropertyBackground, Flat(HudPalette.Background, HudPalette.LineDim)),
            new StyleProperty(Tree.StylePropertyItemBoxSelected, new StyleBoxFlat
            {
                BackgroundColor = HudPalette.FgFaint,
                BorderColor = HudPalette.Fg,
                BorderThickness = new Thickness(2, 0, 0, 0),
                ContentMarginLeftOverride = 4,
            }),
        }));

        var progressBack = Flat(HudPalette.Background, HudPalette.LineDim);
        progressBack.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);
        var progressFore = new StyleBoxFlat { BackgroundColor = HudPalette.FgDim };
        progressFore.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);
        rules.Add(new StyleRule(new SelectorElement(typeof(ProgressBar), null, null, null), new[]
        {
            new StyleProperty(ProgressBar.StylePropertyBackground, progressBack),
            new StyleProperty(ProgressBar.StylePropertyForeground, progressFore),
        }));

        rules.Add(new StyleRule(new SelectorElement(typeof(TextureRect), new[] { CheckBox.StyleClassCheckBox }, null, null), new[]
        {
            new StyleProperty(Control.StylePropertyModulateSelf, HudPalette.FgDim),
        }));
        rules.Add(new StyleRule(new SelectorElement(typeof(TextureRect), new[] { CheckBox.StyleClassCheckBox, CheckBox.StyleClassCheckBoxChecked }, null, null), new[]
        {
            new StyleProperty(Control.StylePropertyModulateSelf, HudPalette.Fg),
        }));

        var grabV = new StyleBoxFlat { BackgroundColor = HudPalette.Border, ContentMarginLeftOverride = 10, ContentMarginTopOverride = 10 };
        var grabVHover = new StyleBoxFlat { BackgroundColor = HudPalette.FgDim, ContentMarginLeftOverride = 10, ContentMarginTopOverride = 10 };
        var grabVActive = new StyleBoxFlat { BackgroundColor = HudPalette.Fg, ContentMarginLeftOverride = 10, ContentMarginTopOverride = 10 };
        var grabH = new StyleBoxFlat { BackgroundColor = HudPalette.Border, ContentMarginTopOverride = 10 };
        var grabHHover = new StyleBoxFlat { BackgroundColor = HudPalette.FgDim, ContentMarginTopOverride = 10 };
        var grabHActive = new StyleBoxFlat { BackgroundColor = HudPalette.Fg, ContentMarginTopOverride = 10 };
        rules.Add(Element<VScrollBar>().Prop(ScrollBar.StylePropertyGrabber, grabV));
        rules.Add(Element<VScrollBar>().Pseudo(ScrollBar.StylePseudoClassHover).Prop(ScrollBar.StylePropertyGrabber, grabVHover));
        rules.Add(Element<VScrollBar>().Pseudo(ScrollBar.StylePseudoClassGrabbed).Prop(ScrollBar.StylePropertyGrabber, grabVActive));
        rules.Add(Element<HScrollBar>().Prop(ScrollBar.StylePropertyGrabber, grabH));
        rules.Add(Element<HScrollBar>().Pseudo(ScrollBar.StylePseudoClassHover).Prop(ScrollBar.StylePropertyGrabber, grabHHover));
        rules.Add(Element<HScrollBar>().Pseudo(ScrollBar.StylePseudoClassGrabbed).Prop(ScrollBar.StylePropertyGrabber, grabHActive));
    }

    private static void AddInputs(List<StyleRule> rules, IResourceCache res)
    {
        var input = Flat(HudPalette.Background, HudPalette.Border);
        input.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        input.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        rules.Add(new StyleRule(new SelectorElement(typeof(LineEdit), null, null, null), new[]
        {
            new StyleProperty(LineEdit.StylePropertyStyleBox, input),
            new StyleProperty("font", LobbyHudStyles.UiFont(res, 12, "Regular")),
        }));
        rules.Add(new StyleRule(new SelectorElement(typeof(LineEdit), null, null, new[] { LineEdit.StylePseudoClassPlaceholder }), new[]
        {
            new StyleProperty("font-color", HudPalette.TextDim),
        }));

        var sliderGrab = new StyleBoxFlat { BackgroundColor = HudPalette.Fg };
        sliderGrab.SetContentMarginOverride(StyleBox.Margin.Horizontal, 3);
        sliderGrab.SetContentMarginOverride(StyleBox.Margin.Vertical, 9);
        rules.Add(new StyleRule(SelectorElement.Type(typeof(Slider)), new[]
        {
            new StyleProperty(Slider.StylePropertyBackground, Flat(HudPalette.Background, HudPalette.Border)),
            new StyleProperty(Slider.StylePropertyForeground, new StyleBoxFlat { BackgroundColor = Color.Transparent, BorderColor = HudPalette.Border, BorderThickness = new Thickness(1) }),
            new StyleProperty(Slider.StylePropertyFill, new StyleBoxFlat { BackgroundColor = HudPalette.FgDim }),
            new StyleProperty(Slider.StylePropertyGrabber, sliderGrab),
        }));
    }

    private static void AddLabels(List<StyleRule> rules, IResourceCache res)
    {
        var heading = LobbyHudStyles.MonoMediumFont(res, 13);
        var bigger = LobbyHudStyles.MonoMediumFont(res, 16);
        var key = LobbyHudStyles.MonoMediumFont(res, 12);
        var mono = LobbyHudStyles.MonoFont(res, 11);
        var small = LobbyHudStyles.MonoFont(res, 10);

        rules.Add(Element<Label>().Class(StyleBase.StyleClassLabelHeading)
            .Prop(Label.StylePropertyFont, heading)
            .Prop(Label.StylePropertyFontColor, HudPalette.Advisory));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassLabelHeadingBigger)
            .Prop(Label.StylePropertyFont, bigger)
            .Prop(Label.StylePropertyFontColor, HudPalette.Advisory));
        rules.Add(Element<RichTextLabel>().Class(StyleBase.StyleClassLabelHeading)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.Advisory));
        rules.Add(Element<RichTextLabel>().Class(StyleNano.StyleClassLabelHeadingBigger)
            .Prop(Control.StylePropertyModulateSelf, HudPalette.Advisory));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassLabelKeyText)
            .Prop(Label.StylePropertyFont, LobbyHudStyles.UiFont(res, 12))
            .Prop(Label.StylePropertyFontColor, HudPalette.Fg));
        rules.Add(Element<Label>().Class(StyleBase.StyleClassLabelSubText)
            .Prop(Label.StylePropertyFont, small)
            .Prop(Label.StylePropertyFontColor, HudPalette.TextDim));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassLabelSecondaryColor)
            .Prop(Label.StylePropertyFontColor, HudPalette.TextDim));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassConsoleText)
            .Prop(Label.StylePropertyFont, mono));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassConsoleSubHeading)
            .Prop(Label.StylePropertyFont, key));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassConsoleHeading)
            .Prop(Label.StylePropertyFont, heading));
        rules.Add(Element<Label>().Class("StatusFieldTitle")
            .Prop(Label.StylePropertyFont, key)
            .Prop(Label.StylePropertyFontColor, HudPalette.FgLabel));
        rules.Add(Element<Label>().Class("Good").Prop(Label.StylePropertyFontColor, HudPalette.Good));
        rules.Add(Element<Label>().Class("Caution").Prop(Label.StylePropertyFontColor, HudPalette.Caution));
        rules.Add(Element<Label>().Class("Danger").Prop(Label.StylePropertyFontColor, HudPalette.Bad));
        rules.Add(Element<Label>().Class("Disabled").Prop(Label.StylePropertyFontColor, HudPalette.Offline));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassPowerStateNone).Prop(Label.StylePropertyFontColor, HudPalette.Bad));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassPowerStateLow).Prop(Label.StylePropertyFontColor, HudPalette.Caution));
        rules.Add(Element<Label>().Class(StyleNano.StyleClassPowerStateGood).Prop(Label.StylePropertyFontColor, HudPalette.Good));
        rules.Add(Element<Label>().Class("WindowFooterText")
            .Prop(Label.StylePropertyFont, small)
            .Prop(Label.StylePropertyFontColor, HudPalette.TextDim));
        rules.Add(Element<TextureRect>().Class("NTLogoDark")
            .Prop(Control.StylePropertyModulateSelf, HudPalette.Border));
    }

    private static void AddButtons(List<StyleRule> rules, string cls, Color lamp, Color text, Font font, bool shapes, bool lit = false)
    {
        foreach (var pseudo in Pseudos)
        {
            var box = ButtonBox(lamp, pseudo, lit);
            rules.Add(Element<ContainerButton>().Class(cls).Pseudo(pseudo)
                .Prop(ContainerButton.StylePropertyStyleBox, box)
                .Prop(Control.StylePropertyModulateSelf, Color.White));

            if (cls != ContainerButton.StyleClassButton)
            {
                rules.Add(Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(cls).Pseudo(pseudo)
                    .Prop(ContainerButton.StylePropertyStyleBox, box)
                    .Prop(Control.StylePropertyModulateSelf, Color.White));
            }

            var color = pseudo switch
            {
                ContainerButton.StylePseudoClassDisabled => HudPalette.Offline,
                ContainerButton.StylePseudoClassHover or ContainerButton.StylePseudoClassPressed => lamp,
                _ => text,
            };
            rules.Add(new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(ContainerButton), new[] { cls }, null, new[] { pseudo }),
                    new SelectorElement(typeof(Label), null, null, null)),
                new[] { new StyleProperty(Label.StylePropertyFontColor, color) }));
        }

        rules.Add(new StyleRule(new SelectorChild(
                new SelectorElement(typeof(ContainerButton), new[] { cls }, null, null),
                new SelectorElement(typeof(Label), null, null, null)),
            new[] { new StyleProperty(Label.StylePropertyFont, font) }));
    }

    private static StyleBox ButtonBox(Color lamp, string pseudo, bool lit = false)
    {
        return pseudo switch
        {
            ContainerButton.StylePseudoClassHover => Lamp(lamp, lit ? StyleBoxLamp.Lit.On : StyleBoxLamp.Lit.Off, lamp == HudPalette.Fg ? HudPalette.BorderBright : lamp),
            ContainerButton.StylePseudoClassPressed => Lamp(lamp, StyleBoxLamp.Lit.On),
            ContainerButton.StylePseudoClassDisabled => Lamp(HudPalette.Offline, StyleBoxLamp.Lit.Off, HudPalette.LineDim),
            _ => Lamp(lamp, lit ? StyleBoxLamp.Lit.On : StyleBoxLamp.Lit.Off, lamp == HudPalette.Fg ? null : StyleBoxLamp.Mix(lamp, HudPalette.Border, 0.45f)),
        };
    }

    public static StyleBoxLamp Lamp(Color lamp, StyleBoxLamp.Lit lit, Color? caseColor = null)
    {
        var box = new StyleBoxLamp(lamp, lit, caseColor);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        return box;
    }

    public static StyleBoxHud WindowFrame(Color? accent)
    {
        var box = StyleBoxHud.Frame(null, null, 0);
        if (accent is { } color)
        {
            box.BorderColor = StyleBoxLamp.Mix(color, HudPalette.Border, 0.55f);
            box.InnerColor = StyleBoxLamp.Mix(color, HudPalette.LineDim, 0.25f);
        }

        return box;
    }

    public static StyleBoxHudHeader Header(Color? accent, bool padded)
    {
        var box = new StyleBoxHudHeader();
        if (accent is { } color)
        {
            box.BackgroundColor = StyleBoxLamp.Mix(color, HudPalette.PanelRaised, 0.12f);
            box.LineColor = StyleBoxLamp.Mix(color, HudPalette.LineDim, 0.45f);
        }

        if (padded)
        {
            box.SetContentMarginOverride(StyleBox.Margin.Left, 8);
            box.SetContentMarginOverride(StyleBox.Margin.Right, 6);
            box.SetContentMarginOverride(StyleBox.Margin.Top, 5);
            box.SetContentMarginOverride(StyleBox.Margin.Bottom, 3);
        }

        return box;
    }

    public static StyleBoxFlat Flat(Color background, Color? border)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border ?? Color.Transparent,
            BorderThickness = new Thickness(border == null ? 0 : 1),
        };
    }
}
