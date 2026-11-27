using System.Numerics;
using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.UserInterface.Hud;

/// <summary>
/// Remembers where each kind of window was left and its size if it was resized, until the round ends.
/// </summary>
public sealed partial class WindowMemoryController : UIController, IOnStateEntered<LobbyState>
{
    private readonly Dictionary<string, (Vector2 Position, Vector2? Size)> _saved = new();
    private readonly Dictionary<BaseWindow, Vector2> _openedSize = new();
    private readonly List<(BaseWindow Window, Color Modulate)> _pending = new();
    private readonly List<BaseWindow> _unmeasured = new();

    public override void Initialize()
    {
        base.Initialize();
        UIManager.WindowRoot.OnChildAdded += OnAdded;
        UIManager.WindowRoot.OnChildRemoved += OnRemoved;
    }

    public void OnStateEntered(LobbyState state)
    {
        _saved.Clear();
        _openedSize.Clear();
        _pending.Clear();
    }

    private static string Key(BaseWindow window) => window.GetType().FullName ?? window.GetType().Name;

    private static bool Skip(Control control) => control is StorageWindow;

    private void OnAdded(Control control)
    {
        if (control is not BaseWindow window || Skip(window))
            return;

        if (!_saved.ContainsKey(Key(window)))
        {
            _openedSize[window] = Vector2.Zero;
            return;
        }

        // the window may still center itself right after this, so it stays transparent until the next frame update
        Restore(window);
        _pending.Add((window, window.Modulate));
        window.Modulate = window.Modulate.WithAlpha(0);
    }

    private void OnRemoved(Control control)
    {
        if (control is not BaseWindow window || Skip(window))
            return;

        var index = _pending.FindIndex(p => p.Window == window);
        if (index >= 0)
        {
            window.Modulate = _pending[index].Modulate;
            _pending.RemoveAt(index);
        }

        if (!_openedSize.Remove(window, out var opened))
            return;

        var key = Key(window);
        Vector2? size = (window.Size - opened).Length() > 1f
            ? window.Size
            : _saved.TryGetValue(key, out var old) ? old.Size : null;
        _saved[key] = (window.Position, size);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        foreach (var (window, modulate) in _pending)
        {
            if (window.Disposed || window.Parent != UIManager.WindowRoot)
                continue;

            Restore(window);
            _openedSize[window] = Vector2.Zero;
            window.Modulate = modulate;
        }

        _pending.Clear();

        // the size a window opened with is known once it has been laid out
        _unmeasured.Clear();
        foreach (var (window, size) in _openedSize)
        {
            if (size == Vector2.Zero && window.Size != Vector2.Zero)
                _unmeasured.Add(window);
        }

        foreach (var window in _unmeasured)
            _openedSize[window] = window.Size;
    }

    private void Restore(BaseWindow window)
    {
        if (!_saved.TryGetValue(Key(window), out var saved))
            return;

        var screen = UIManager.RootControl.Size;
        var size = window.Size;
        if (saved.Size is { } wanted && window.Resizable)
        {
            size = Vector2.Min(Vector2.Max(wanted, window.MinSize), screen);
            window.SetSize = size;
        }

        var position = Vector2.Clamp(saved.Position, Vector2.Zero, Vector2.Max(screen - size, Vector2.Zero));
        LayoutContainer.SetPosition(window, position);
    }
}
