using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Lua.Styles;

public static class LunaWindowStyle
{
    public static readonly Color FrameBg = Color.FromHex("#0B0F14");
    public static readonly Color FrameBorder = Color.FromHex("#243041");
    public static readonly Color TitleBarBg = Color.FromHex("#0E1218");
    public static readonly Color PanelBg = Color.FromHex("#0E1218");
    public static readonly Color PanelBorder = Color.FromHex("#2A3344");
    public static readonly Color Divider = Color.FromHex("#1C2433");

    public static readonly Color TextMuted = Color.FromHex("#7E8BA0");
    public static readonly Color TextSecondary = Color.FromHex("#8A96A8");
    public static readonly Color TextPrimary = Color.FromHex("#C5CEDB");

    public static readonly Color Accent = Color.FromHex("#5EC8E8");
    public static readonly Color AccentWarn = Color.FromHex("#E8A43A");
    public static readonly Color AccentBad = Color.FromHex("#E07070");

    private static Font? _fontSmall;
    private static Font? _fontBody;
    private static Font? _fontTitle;
    private static Font? _fontFooter;

    public static Font FontSmall => _fontSmall ??= Cache().NotoStack(size: 10);
    public static Font FontBody => _fontBody ??= Cache().NotoStack(size: 12);
    public static Font FontTitle => _fontTitle ??= Cache().NotoStack(variation: "Bold", size: 13);
    public static Font FontFooter => _fontFooter ??= Cache().NotoStack(size: 8);

    private static IResourceCache Cache() => IoCManager.Resolve<IResourceCache>();

    public static StyleBoxFlat Box(Color background, Color border, float borderThickness = 1f) => new()
    {
        BackgroundColor = background,
        BorderColor = border,
        BorderThickness = new Thickness(borderThickness),
    };

    public static StyleBoxFlat Shell() => Box(FrameBg, FrameBorder);

    public static StyleBoxFlat ThinDivider() => new()
    {
        BackgroundColor = Divider,
        BorderColor = Color.Transparent,
        BorderThickness = new Thickness(0),
        ContentMarginTopOverride = 1,
        ContentMarginBottomOverride = 1,
    };

    public static void ApplyWindowChrome(FancyWindow window)
    {
        if (window.ChildCount < 2)
            return;

        if (window.GetChild(0) is PanelContainer frame)
        {
            frame.ModulateSelfOverride = Color.White;
            frame.PanelOverride = Box(FrameBg, FrameBorder);
        }

        if (window.GetChild(1) is not BoxContainer layout || layout.ChildCount < 2)
            return;

        var titleHost = layout.GetChild(0);
        if (titleHost.ChildCount > 0 && titleHost.GetChild(0) is PanelContainer titlePanel)
        {
            titlePanel.ModulateSelfOverride = Color.White;
            titlePanel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = TitleBarBg,
                BorderColor = FrameBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
        }

        if (titleHost.ChildCount > 1 &&
            titleHost.GetChild(1) is BoxContainer titleRow &&
            titleRow.ChildCount > 0 &&
            titleRow.GetChild(0) is Label titleLabel)
        {
            titleLabel.FontOverride = FontTitle;
            titleLabel.FontColorOverride = TextPrimary;
        }

        if (layout.GetChild(1) is PanelContainer divider)
            divider.PanelOverride = ThinDivider();
    }

    public static void StyleDivider(PanelContainer panel) => panel.PanelOverride = ThinDivider();

    public static void StyleMuted(Label label) => StyleSmall(label, TextMuted);

    public static void StyleSecondary(Label label) => StyleSmall(label, TextSecondary);

    public static void StyleValue(Label label) => StyleSmall(label, Accent);

    public static void StyleFooter(Label label)
    {
        label.FontOverride = FontFooter;
        label.FontColorOverride = TextMuted;
    }

    private static void StyleSmall(Label label, Color color)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontSmall;
        label.FontColorOverride = color;
    }
}
