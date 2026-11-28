using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed partial class HudWindowAccentController : UIController
{
    private static readonly string[] Frames =
    {
        DefaultWindow.StyleClassWindowPanel,
        StyleBase.ClassAngleRect,
        DefaultWindow.StyleClassWindowHeader,
        "WindowHeadingBackground",
        "WindowHeadingBackgroundLight",
        DeadSpaceAHelpStyles.Shell,
        DeadSpaceAHelpStyles.Header,
    };

    private static readonly string[] Titles =
    {
        DefaultWindow.StyleClassWindowTitle,
        "FancyWindowTitle",
        DeadSpaceAHelpStyles.Title,
    };

    public override void Initialize()
    {
        base.Initialize();
        UIManager.WindowRoot.OnChildAdded += OnWindowAdded;
    }

    [Dependency] private Robust.Client.Player.IPlayerManager _player = default!;
    [Dependency] private Robust.Shared.Prototypes.IPrototypeManager _protos = default!;
    [Dependency] private IEntityManager _entities = default!;

    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;
    private readonly Dictionary<(EntityUid, Enum), TimeSpan> _seen = new();
    private readonly HashSet<(EntityUid, Enum)> _currentUis = new();
    private readonly List<(EntityUid, Enum)> _stale = new();
    private readonly List<(BaseWindow Window, TimeSpan Added)> _pending = new();

    /// <summary>
    /// The accent of the entity a window was opened from, kept on the window itself so a reopened window keeps it.
    /// A dictionary here kept up to 200 closed windows with all their controls alive.
    /// </summary>
    private static readonly AttachedProperty<HudAccent?> OwnerAccent =
        AttachedProperty<HudAccent?>.Create("LuaMHudOwnerAccent", typeof(HudWindowAccentController));

    public override void FrameUpdate(Robust.Shared.Timing.FrameEventArgs args)
    {
        base.FrameUpdate(args);

        MatchOwners();
        FitWindows();
    }

    private void MatchOwners()
    {
        var now = _timing.RealTime;
        _currentUis.Clear();

        // entity systems are not running in the main menu (and in client tests before connecting),
        // System<T>() threw there every frame a window was waiting for its owner
        _entities.EntitySysManager.TryGetEntitySystem<Robust.Client.GameObjects.UserInterfaceSystem>(out var uiSystem);

        // read the component directly: GetActorUis is an iterator and allocated every frame
        if (uiSystem != null && _entities.TryGetComponent<UserInterfaceUserComponent>(_player.LocalEntity, out var user))
        {
            foreach (var (uid, keys) in user.OpenInterfaces)
            {
                foreach (var key in keys)
                {
                    _currentUis.Add((uid, key));
                    _seen.TryAdd((uid, key), now);
                }
            }
        }

        if (_seen.Count > _currentUis.Count)
        {
            _stale.Clear();
            foreach (var key in _seen.Keys)
            {
                if (!_currentUis.Contains(key))
                    _stale.Add(key);
            }

            foreach (var key in _stale)
                _seen.Remove(key);
        }

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var (window, added) = _pending[i];
            (EntityUid, Enum)? best = null;
            var bestGap = TimeSpan.MaxValue;
            var windowName = window.GetType().Name;
            foreach (var (key, seen) in _seen)
            {
                var gap = (seen - added).Duration();
                if (uiSystem != null && uiSystem.TryGetOpenUi(key.Item1, key.Item2, out var bui))
                {
                    var stem = bui.GetType().Name.Replace("BoundUserInterface", string.Empty).Replace("BUI", string.Empty);
                    if (stem.Length > 3 && windowName.StartsWith(stem, StringComparison.Ordinal))
                        gap -= TimeSpan.FromSeconds(10);
                }

                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = key;
                }
            }

            if (best is { } owner && bestGap < TimeSpan.FromSeconds(0.5))
            {
                var proto = _entities.GetComponentOrNull<MetaDataComponent>(owner.Item1)?.EntityPrototype?.ID;
                var resolved = HudAccents.ForPrototype(proto, _protos) ?? HudAccents.For(window.GetType());
                window.SetValue<HudAccent?>(OwnerAccent, resolved);
                Decorate(window, resolved);
                _pending.RemoveAt(i);
            }
            else if (now - added > TimeSpan.FromSeconds(1))
            {
                _pending.RemoveAt(i);
            }
        }
    }

    private readonly List<(BaseWindow Window, int Frames)> _fit = new();
    private void FitWindows()
    {
        for (var i = _fit.Count - 1; i >= 0; i--)
        {
            var (window, frames) = _fit[i];
            if (frames < 1)
            {
                _fit[i] = (window, frames + 1);
                continue;
            }

            _fit.RemoveAt(i);
            if (window.Disposed || !window.IsOpen)
                continue;

            var screen = UIManager.RootControl.Size;
            var max = screen * 0.92f;
            var size = window.Size;
            if (size.X <= max.X && size.Y <= max.Y && window.Position.X >= 0 && window.Position.Y >= 0
                && window.Position.X + size.X <= screen.X && window.Position.Y + size.Y <= screen.Y)
                continue;

            var fitted = size;
            if (size.X > max.X || size.Y > max.Y)
            {
                fitted = System.Numerics.Vector2.Max(System.Numerics.Vector2.Min(size, max), window.MinSize);
                window.SetSize = fitted;
            }

            var position = System.Numerics.Vector2.Clamp(window.Position, System.Numerics.Vector2.Zero, System.Numerics.Vector2.Max(screen - fitted, System.Numerics.Vector2.Zero));
            if (position != window.Position)
                LayoutContainer.SetPosition(window, position);
        }
    }

    private void OnWindowAdded(Control control)
    {
        if (control is not BaseWindow window)
            return;

        if (window is not Content.Client.UserInterface.Systems.Storage.Controls.StorageWindow)
            _fit.Add((window, 0));

        if (window.GetValue(OwnerAccent) is { } known)
        {
            Decorate(window, known);
            return;
        }

        Decorate(window, HudAccents.For(window.GetType()));
        _pending.Add((window, _timing.RealTime));
    }

    public static void Decorate(BaseWindow window, HudAccent accent)
    {
        var cls = accent == HudAccent.Default ? null : HudAccents.Class(accent);
        Walk(window, cls, 0);
    }

    private static void Walk(Control control, string? cls, int depth)
    {
        if (depth > 5)
            return;

        foreach (var child in new List<Control>(control.Children))
        {
            switch (child)
            {
                case PanelContainer panel when HasAny(panel, Frames):
                    Tag(panel, cls);
                    break;
                case Label label when depth <= 3 && HasAny(label, Titles):
                    Tag(label, cls);
                    if (!label.HasStyleClass(HudThemeStyles.WindowTitleClass))
                        label.AddStyleClass(HudThemeStyles.WindowTitleClass);
                    AddSquare(label, cls);
                    break;
            }

            if (child is not BaseWindow)
                Walk(child, cls, depth + 1);
        }
    }

    private static void AddSquare(Label label, string? cls)
    {
        if (label.Parent is not BoxContainer row)
            return;

        var index = label.GetPositionInParent();
        if (index > 0 && row.GetChild(index - 1) is HudSquare existing)
        {
            Tag(existing, cls);
            return;
        }

        var square = new HudSquare { Margin = new Thickness(4, 0, 0, 0) };
        Tag(square, cls);
        row.AddChild(square);
        square.SetPositionInParent(index);
        if (label.Margin.Left < 6)
            label.Margin = new Thickness(6, label.Margin.Top, label.Margin.Right, label.Margin.Bottom);
    }

    private static void Tag(Control control, string? cls)
    {
        foreach (var accent in HudAccents.Tinted)
        {
            var other = HudAccents.Class(accent);
            if (other != cls && control.HasStyleClass(other))
                control.RemoveStyleClass(other);
        }

        if (cls != null && !control.HasStyleClass(cls))
            control.AddStyleClass(cls);
    }

    private static bool HasAny(Control control, string[] classes)
    {
        foreach (var cls in classes)
        {
            if (control.HasStyleClass(cls))
                return true;
        }

        return false;
    }
}
