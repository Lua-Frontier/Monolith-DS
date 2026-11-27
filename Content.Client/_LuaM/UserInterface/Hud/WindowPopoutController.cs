using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed partial class WindowPopoutController : UIController
{
    private const string ButtonName = "LuaMPopoutButton";
    private static readonly ResPath Icon = new("/Textures/_LuaM/Interface/Hud/popout.png");

    private readonly Dictionary<BaseWindow, Popout> _popped = new();
    private readonly List<BaseWindow> _toPopIn = new();

    private sealed class Popout
    {
        public required OSWindow Os;
        public required TextureButton Button;
        public Vector2 Position;
        public Vector2 SetSize;
    }

    public override void Initialize()
    {
        base.Initialize();
        UIManager.WindowRoot.OnChildAdded += OnWindowAdded;
    }

    private void OnWindowAdded(Control control)
    {
        if (control is not BaseWindow window || FindButton(window, ButtonName) != null)
            return;

        if (FindCloseButton(window, 0) is not { Parent: { } row } close)
            return;

        var button = new TextureButton
        {
            Name = ButtonName,
            TexturePath = Icon.ToString(),
            VerticalAlignment = Control.VAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            ModulateSelfOverride = HudPalette.TextDim,
            ToolTip = Loc.GetString("luam-window-popout"),
        };
        button.OnMouseEntered += _ => button.ModulateSelfOverride = HudPalette.Fg;
        button.OnMouseExited += _ => button.ModulateSelfOverride = HudPalette.TextDim;
        button.OnPressed += _ => Toggle(window, button);

        var index = close.GetPositionInParent();
        row.AddChild(button);
        button.SetPositionInParent(index);
    }

    private static TextureButton? FindCloseButton(Control control, int depth)
    {
        if (depth > 5)
            return null;

        foreach (var child in control.Children)
        {
            if (child is TextureButton button && button.HasStyleClass(DefaultWindow.StyleClassWindowCloseButton))
                return button;

            if (child is BaseWindow)
                continue;

            if (FindCloseButton(child, depth + 1) is { } found)
                return found;
        }

        return null;
    }

    private static Control? FindButton(Control control, string name)
    {
        foreach (var child in control.Children)
        {
            if (child.Name == name)
                return child;

            if (child is BaseWindow)
                continue;

            if (FindButton(child, name) is { } found)
                return found;
        }

        return null;
    }

    private void Toggle(BaseWindow window, TextureButton button)
    {
        if (_popped.ContainsKey(window))
            PopIn(window);
        else
            PopOut(window, button);
    }

    private void PopOut(BaseWindow window, TextureButton button)
    {
        if (!window.IsOpen)
            return;

        var title = window switch
        {
            DefaultWindow d => d.Title,
            FancyWindow f => f.Title,
            _ => null,
        };

        var os = new OSWindow
        {
            SetSize = window.Size,
            Title = string.IsNullOrWhiteSpace(title) ? Loc.GetString("luam-window-popout-title") : title,
        };

        var popout = new Popout
        {
            Os = os,
            Button = button,
            Position = window.Position,
            SetSize = window.SetSize,
        };

        os.Show();
        window.Orphan();
        window.SetSize = new Vector2(float.NaN, float.NaN);
        os.AddChild(window);
        _popped[window] = popout;

        button.ToolTip = Loc.GetString("luam-window-popin");

        os.Closed += () => PopIn(window);
        os.OnChildRemoved += child =>
        {
            if (child == window && _popped.ContainsKey(window))
            {
                _popped.Remove(window);
                Reset(popout);
                popout.Os.Close();
            }
        };
    }

    private void PopIn(BaseWindow window)
    {
        if (!_popped.Remove(window, out var popout))
            return;

        Reset(popout);
        window.SetSize = popout.SetSize;

        if (window.Parent != null)
            window.Orphan();

        if (!window.Disposed)
        {
            UIManager.WindowRoot.AddChild(window);
            var screen = UIManager.RootControl.Size;
            var size = window.Size == Vector2.Zero ? popout.Os.Size : window.Size;
            LayoutContainer.SetPosition(window, Vector2.Clamp(popout.Position, Vector2.Zero, Vector2.Max(screen - size, Vector2.Zero)));
        }

        popout.Os.Close();
    }

    private static void Reset(Popout popout)
    {
        popout.Button.ToolTip = Loc.GetString("luam-window-popout");
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_popped.Count == 0)
            return;

        _toPopIn.Clear();
        foreach (var (window, popout) in _popped)
        {
            if (window.Disposed || popout.Os.ClydeWindow == null)
            {
                _toPopIn.Add(window);
                continue;
            }

            if (!float.IsNaN(window.SetSize.X) || !float.IsNaN(window.SetSize.Y))
                window.SetSize = new Vector2(float.NaN, float.NaN);
        }

        foreach (var window in _toPopIn)
        {
            PopIn(window);
        }
    }
}
