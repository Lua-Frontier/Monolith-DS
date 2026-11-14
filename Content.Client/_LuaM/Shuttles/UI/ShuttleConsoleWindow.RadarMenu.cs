using Content.Shared._LuaM.Shuttles.Events;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    public event Action<NetEntity>? OnRadarMenuRequest;
    public event Action<NetEntity>? OnRadarFollow;
    public event Action<NetEntity>? OnRadarCall;

    private void InitializeRadarMenu()
    {
        NavContainer.OnRadarMenuRequest += grid => OnRadarMenuRequest?.Invoke(grid);
        NavContainer.OnRadarFollow += grid => OnRadarFollow?.Invoke(grid);
        NavContainer.OnRadarCall += target => OnRadarCall?.Invoke(target);
        MapContainer.OnRadarMenuRequest += grid => OnRadarMenuRequest?.Invoke(grid);
        MapContainer.OnRadarFollow += grid => OnRadarFollow?.Invoke(grid);
        MapContainer.OnRadarCall += target => OnRadarCall?.Invoke(target);
    }

    public void ShowRadarMenu(ShuttleRadarMenuMessage msg)
    {
        NavContainer.ShowRadarMenu(msg);
        MapContainer.ShowRadarMenu(msg);
    }
}
