/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using Content.Client._LuaM.Shipyard.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Lazy map plans using the same client renderer and cache as the shipyard.</summary>
public sealed partial class GuidebookMapPreviewSystem : EntitySystem
{
    [Dependency] private IClientNetManager _net = default!;
    private FancyWindow? _window;
    private ShipyardPreviewView? _view;
    private Label? _status;
    public bool IsLoading => _view?.Loading == true;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Close());
        _net.Disconnect += Disconnected;
    }
    private void Disconnected(object? sender, NetDisconnectedArgs args) => Close();
    public override void Shutdown()
    {
        _net.Disconnect -= Disconnected;
        Close();
        base.Shutdown();
    }
    public void Open(string path, string name)
    {
        Close();
        var window = new FancyWindow { Title = name, MinSize = new(480, 360), SetSize = new(1000, 760) };
        _window = window;
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        window.ContentsContainer.AddChild(body);
        _status = new Label { Text = "Подготовка плана на клиенте…" };
        body.AddChild(_status);
        var view = new ShipyardPreviewView();
        _view = view;
        view.StateChanged += UpdateStatus;
        view.SetMap(path);
        body.AddChild(view);
        var fit = new Button { Text = "Вписать целиком" };
        fit.OnPressed += _ => view.FitWhole();
        body.AddChild(fit);
        body.AddChild(new Label { Text = "Колесо — масштаб · ЛКМ/ПКМ — перемещение плана" });
        window.OnClose += Close;

        window.OpenCentered();
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        if (_status == null || _view == null) return;
        if (_view.Loading) { _status.Text = "Загрузка карты на клиенте…"; return; }
        if (_view.Entry is not { } entry) { _status.Text = "Подготовка плана…"; return; }
        if (entry.Failed)
        {
            _status.Text = entry.Error.Length > 0 ? entry.Error : "Не удалось подготовить карту: неподдерживаемая структура или превышен лимит размера/сущностей.";
            return;
        }
        _status.Text = $"Гридов: {entry.Info?.Grids} · {entry.Info?.Dimensions.X}×{entry.Info?.Dimensions.Y} тайлов · {entry.PixelsPerTile} px/тайл"
            + (entry.PixelsPerTile < 32 ? " · разрешение уменьшено для ограничения памяти" : "");
    }
    public void Close()
    {
        var window = _window;
        if (window == null) return;
        _window = null;
        if (_view != null) _view.StateChanged -= UpdateStatus;
        _view = null; _status = null;
        window.OnClose -= Close;
        window.Dispose();
    }
}
