using Robust.Shared.Maths;

namespace Content.Client.Stylesheets;

/// <summary>
/// Monolith-DS interface colors: the console kit colors.
/// </summary>
public static class LuaMPalette
{
    /// <summary>
    /// Window and panel background.
    /// </summary>
    public static readonly Color Panel = Color.FromHex("#121618");

    /// <summary>
    /// Darker inset: lists, inputs, trees.
    /// </summary>
    public static readonly Color PanelDeep = Color.FromHex("#07090A");

    /// <summary>
    /// Slightly lighter rows and secondary panels.
    /// </summary>
    public static readonly Color PanelRaised = Color.FromHex("#1A1F22");

    public static readonly Color Header = Color.FromHex("#1A1F22");
    public static readonly Color Border = Color.FromHex("#3D484D");

    /// <summary>
    /// Headings, window titles and other highlighted text.
    /// </summary>
    public static readonly Color AccentText = Color.FromHex("#7FD1C7");

    public static readonly Color Accent = Color.FromHex("#4A7D77");

    public static readonly Color Button = Color.FromHex("#121618");
    public static readonly Color ButtonHover = Color.FromHex("#1F2629");
    public static readonly Color ButtonPressed = Color.FromHex("#24403D");
    public static readonly Color ButtonDisabled = Color.FromHex("#0A0C0D");

    /// <summary>
    /// Selected list row or active tab.
    /// </summary>
    public static readonly Color Selected = Color.FromHex("#24403D");

    public static readonly Color Scrollbar = Color.FromHex("#3D484D");
    public static readonly Color ScrollbarHover = Color.FromHex("#4A7D77");
}
