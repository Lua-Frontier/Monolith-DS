namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    private void UpdateHints(ShuttleConsoleMode mode)
    {
        HintsLabel.Text = mode switch
        {
            ShuttleConsoleMode.Map => Loc.GetString("shuttle-console-hints-map"),
            ShuttleConsoleMode.Dock => Loc.GetString("shuttle-console-hints-dock"),
            _ => Loc.GetString("shuttle-console-hints-nav"),
        };
    }
}
