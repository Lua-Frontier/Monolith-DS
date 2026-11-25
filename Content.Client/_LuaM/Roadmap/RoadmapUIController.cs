using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._LuaM.Roadmap;

[UsedImplicitly]
public sealed class RoadmapUIController : UIController
{
    private RoadmapWindow? _window;

    public void OpenWindow()
    {
        if (_window == null || _window.Disposed)
        {
            _window = UIManager.CreateWindow<RoadmapWindow>();
            _window.OnClose += () => _window = null;
        }

        if (_window.IsOpen)
            _window.MoveToFront();
        else
            _window.OpenCentered();
    }

    public void ToggleWindow()
    {
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        OpenWindow();
    }
}
