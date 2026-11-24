namespace Content.Client._LuaM.UserInterface.Hud;

public static class HudPalette
{
    public static readonly Color Void = Color.FromHex("#0A0C0D");
    public static readonly Color Background = Color.FromHex("#07090A");
    public static readonly Color Panel = Color.FromHex("#121618");
    public static readonly Color PanelRaised = Color.FromHex("#1A1F22");
    public static readonly Color PanelHover = Color.FromHex("#1F2629");

    public static readonly Color LineDim = Color.FromHex("#2A3236");
    public static readonly Color Border = Color.FromHex("#3D484D");
    public static readonly Color BorderBright = Color.FromHex("#5A686E");

    public static readonly Color Text = Color.FromHex("#C9CFD1");
    public static readonly Color TextDim = Color.FromHex("#7F898C");
    public static readonly Color Cream = Color.FromHex("#DCE2E3");

    public static readonly Color Fg = Color.FromHex("#7FD1C7");
    public static readonly Color FgDim = Color.FromHex("#4A7D77");
    public static readonly Color FgFaint = Color.FromHex("#24403D");
    public static readonly Color FgLabel = Color.FromHex("#5A938C");

    public static readonly Color Good = Color.FromHex("#6FBF73");
    public static readonly Color Advisory = Color.FromHex("#5FA8D3");
    public static readonly Color Caution = Color.FromHex("#E3B341");
    public static readonly Color Bad = Color.FromHex("#E5533D");
    public static readonly Color Offline = Color.FromHex("#4A5256");

    public static readonly Color Orange = Fg;
    public static readonly Color OrangeDark = FgFaint;
    public static readonly Color Cyan = Fg;
    public static readonly Color Sage = FgFaint;
    public static readonly Color SageBright = FgDim;

    public static StyleBoxHud Box(Color background, Color border, Color? accent = null, float chamfer = 0)
    {
        return new StyleBoxHud(background, border, accent) { Chamfer = chamfer };
    }
}
