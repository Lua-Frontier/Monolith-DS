/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._Mono.FireControl.UI;
using Content.Client._Goobstation.Research.UI;
using Content.Shared._Goobstation.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Content.Client.Guidebook.Richtext;
using Content.Client.Salvage.UI;
using Content.Client.Shuttles.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.ShipGuns;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Systems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Guidebook;

/// <summary>
/// Native console windows populated solely with local teaching state. No BUI is opened,
/// so their request events have no route to server-side ship or expedition systems.
/// </summary>
[UsedImplicitly]
public sealed partial class GuideNavigationUI : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly List<EntityUid> _dummies = new();
    private FancyWindow? _window;
    private string _mode = string.Empty;
    private Button? _researchButton;

    public GuideNavigationUI()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        Margin = new Thickness(0, 5, 0, 15);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Mode", out var mode) || mode is not ("shuttle" or "weapons" or "expedition" or "research"))
            return false;

        _mode = mode;
        var hint = new RichTextLabel();
        hint.SetMessage("Учебный интерфейс. Изменения действуют только в этом примере.");
        AddChild(hint);
        var open = new Button
        {
            Text = mode switch
            {
                "shuttle" => "Открыть NAV / MAP / DOCK",
                "weapons" => "Открыть учебное управление орудиями",
                "research" => "Открыть учебный РНД",
                _ => "Открыть учебную консоль экспедиций",
            },
            HorizontalExpand = true,
        };
        open.OnPressed += _ => OpenExample();
        if (mode == "research") _researchButton = open;
        AddChild(open);
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_mode != "research") return;
        var catalog = _entities.System<GuidebookCatalogClientSystem>();
        catalog.Updated += ResearchDataChanged;
        catalog.Request(); ResearchDataChanged();
    }

    private void ResearchDataChanged()
    {
        var available = _entities.System<GuidebookCatalogClientSystem>().Catalog != null;
        if (_researchButton != null) _researchButton.Disabled = !available;
        if (!available) CloseExample();
    }

    private void OpenExample()
    {
        CloseExample();
        try
        {
            switch (_mode)
            {
                case "shuttle":
                    CreateShuttle();
                    break;
                case "weapons":
                    CreateWeapons();
                    break;
                case "research":
                    CreateResearch();
                    break;
                default:
                    CreateExpedition();
                    break;
            }

            _window!.HelpGuidebookIds = null;
            _window.OnClose += CloseExample;
            _window.OpenCentered();
        }
        catch
        {
            CloseExample();
            throw;
        }
    }

    private EntityUid CreateDummy()
    {
        var dummy = _entities.SpawnEntity(null, MapCoordinates.Nullspace);
        _dummies.Add(dummy);
        return dummy;
    }

    private static NavInterfaceState GetNav() => new(
        512f, null, Angle.Zero, new Dictionary<NetEntity, List<DockingPortState>>(),
        InertiaDampeningMode.Dampen, true, null, null, 3000f, false);

    private BoxContainer AddInstructions(FancyWindow window, string message)
    {
        var root = (BoxContainer) window.ContentsContainer.Children.First();
        var wrapper = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var instructions = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8, 4),
        };
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        instructions.AddChild(label);
        // Fire control has a horizontal native root. Wrap it instead of inserting a
        // third column, preserving its named controls and usable radar width.
        window.ContentsContainer.RemoveChild(root);
        window.ContentsContainer.AddChild(wrapper);
        wrapper.AddChild(instructions);
        wrapper.AddChild(root);
        root.VerticalExpand = true;
        return instructions;
    }

    private void CreateShuttle()
    {
        var window = new ShuttleConsoleWindow();
        _window = window;
        window.Title = "Учебная консоль — NAV / MAP / DOCK";
        var now = _timing.CurTime;
        var owner = CreateDummy();
        var state = new ShuttleBoundUserInterfaceState(
            GetNav(),
            new ShuttleMapInterfaceState(FTLState.Available,
                new Content.Shared.Timing.StartEndTime(now, now + TimeSpan.FromSeconds(1)), new(), new()),
            new DockingInterfaceState(new()));
        window.UpdateState(owner, state);
        var panel = AddInstructions(window,
            "Переключайте NAV, MAP и DOCK. На MAP найдите поиск, БСС, метку X/Y и автопилот; на DOCK — список соединений. " +
            "Учебный радар пуст: окно не подключено к кораблю или карте смены.");
        var phases = new[] { FTLState.Starting, FTLState.Travelling, FTLState.Arriving, FTLState.Cooldown, FTLState.Available };
        var phase = 0;
        var nextPhase = new Button { Text = "Показать следующий этап БСС на MAP" };
        nextPhase.OnPressed += _ =>
        {
            var start = _timing.CurTime;
            state.MapState = new ShuttleMapInterfaceState(phases[phase],
                new Content.Shared.Timing.StartEndTime(start, start + TimeSpan.FromSeconds(10)), new(), new());
            phase = (phase + 1) % phases.Length;
            window.UpdateState(owner, state);
            window.SwitchMode(ShuttleConsoleWindow.ShuttleConsoleMode.Map);
        };
        panel.AddChild(nextPhase);
    }

    private FireControllableEntry CreateGun(ShipGunType type, string name, int? ammo)
    {
        var dummy = CreateDummy();
        _entities.EnsureComponent<ShipGunTypeComponent>(dummy).Type = type;
        var net = _entities.GetNetEntity(dummy);
        return new FireControllableEntry(net, new NetCoordinates(net, System.Numerics.Vector2.Zero), name, ammo);
    }

    private void CreateWeapons()
    {
        var window = new FireControlWindow();
        _window = window;
        window.Title = "Учебная консоль — управление орудиями";
        var entries = new[]
        {
            CreateGun(ShipGunType.Ballistic, "Учебное баллистическое орудие", 120),
            CreateGun(ShipGunType.Energy, "Учебное энергетическое орудие", null),
            CreateGun(ShipGunType.Missile, "Учебная ракетная установка", 4),
        };
        var state = new FireControlConsoleBoundInterfaceState(true, entries, GetNav());
        window.UpdateStatus(state);
        var panel = AddInstructions(window,
            "Выберите отдельные орудия или категорию, затем снимите выбор. Цифры в списке — пример боезапаса. " +
            "Кнопка ниже показывает, как выглядит отсутствие связи с сервером вооружения.");
        var connection = new Button { Text = "Переключить связь с учебным сервером" };
        connection.OnPressed += _ =>
        {
            state.Connected = !state.Connected;
            window.UpdateStatus(state);
        };
        panel.AddChild(connection);
        window.OnServerRefresh += () =>
        {
            state.Connected = true;
            window.UpdateStatus(state);
        };
    }

    private void CreateResearch()
    {
        var dummy = _entities.SpawnEntity("ResearchAndDevelopmentServerDebug", MapCoordinates.Nullspace);
        _dummies.Add(dummy);
        var supported = _entities.System<GuidebookCatalogClientSystem>().Catalog?.ResearchDisciplines.ToHashSet() ?? new HashSet<string>();
        var window = new FancyResearchConsoleMenu { GuidebookReadOnly = true, GuidebookDisciplines = supported };
        _window = window;
        window.Title = "Учебный РНД — ветки и карточки";
        window.SetEntity(dummy);
        window.UpdatePanels(_prototypes.EnumeratePrototypes<TechnologyPrototype>().Where(t => !t.Hidden
                && t.GetAllDisciplines().Any(d => supported.Contains(d.Id)))
            .ToDictionary(t => t.ID, t => t.TechnologyPrerequisites.Count == 0 ? ResearchAvailability.Available : ResearchAvailability.Unavailable));
        window.UpdateInformationPanel(60000);
        AddInstructions(window, "Выберите ветку, затем значок исследования. Справа — цена, условия и открытия; ссылки ведут к предшественникам. " +
            "Здесь показан пример с 60 000 очков и ветками, доступными серверу. Покупка отключена.");
    }

    private void CreateExpedition()
    {
        var window = new SalvageExpeditionWindow();
        _window = window;
        window.Title = "Учебная консоль — экспедиции";
        var state = new SalvageExpeditionConsoleState(_timing.CurTime, false, false, false, 0,
            new List<SalvageMissionParams>
            {
                new() { Index = 1, MissionType = SalvageMissionType.Elimination, Difficulty = DifficultyRating.Moderate, Seed = 14 },
                new() { Index = 2, MissionType = SalvageMissionType.Destruction, Difficulty = DifficultyRating.Hazardous, Seed = 42 },
            });
        window.UpdateState(state);
        var panel = AddInstructions(window,
            "Сравните сложность, цель, противников, длительность, биом и модификаторы двух примеров. " +
            "Нажмите принятие задания: выбор появится только в учебной строке ниже.");
        var selected = new Label { Text = "Учебное задание ещё не выбрано." };
        panel.AddChild(selected);
        // Do not call UpdateState again: the native window attaches its Finish handler
        // on each update. Keep this teaching example to one update and one local callback.
        window.ClaimMission += index => selected.Text = $"Выбрано учебное задание №{index}. Диск координат и экспедиция не создаются.";
    }

    private void CloseExample()
    {
        var window = _window;
        _window = null;
        if (window != null)
        {
            window.OnClose -= CloseExample;
            if (window.Parent != null) window.Close();
        }

        foreach (var dummy in _dummies)
        {
            if (!_entities.Deleted(dummy))
                _entities.DeleteEntity(dummy);
        }
        _dummies.Clear();
    }

    protected override void ExitedTree()
    {
        _entities.System<GuidebookCatalogClientSystem>().Updated -= ResearchDataChanged;
        CloseExample();
        base.ExitedTree();
    }
}

