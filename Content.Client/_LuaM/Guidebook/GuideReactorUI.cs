/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._FarHorizons.Power.UI;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.Controls;
using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared.Atmos;
using Content.Shared.Lock;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>
/// Opens the actual machine windows against an isolated, client-local teaching entity.
/// No bound interface is created: every subscribed action changes only demonstration state.
/// </summary>
[UsedImplicitly]
public sealed class GuideReactorUI : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IComponentFactory _factory = default!;

    private FancyWindow? _window;
    private EntityUid? _dummy;
    private bool _reactor;

    public GuideReactorUI()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        Margin = new Thickness(0, 5, 0, 15);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Mode", out var mode) || mode is not ("reactor" or "turbine"))
            return false;

        _reactor = mode == "reactor";
        var label = new RichTextLabel();
        label.SetMessage("Учебный интерфейс. Изменения действуют только в этом примере.");
        AddChild(label);
        var open = new Button
        {
            Text = _reactor ? "Открыть учебный реактор" : "Открыть учебную турбину",
            HorizontalExpand = true,
        };
        open.OnPressed += _ => OpenExample();
        AddChild(open);
        control = this;
        return true;
    }

    private void OpenExample()
    {
        CloseExample();
        try
        {
            var entity = _entities.SpawnEntity(_reactor ? "NuclearReactorSmall" : "TurbineSmall", MapCoordinates.Nullspace);
            _dummy = entity;
            // A missing lock is treated as unlocked by the native windows. Removing this
            // component only on our local entity avoids lock audio, prediction and access checks.
            _entities.RemoveComponent<LockComponent>(entity);

            if (_reactor)
                CreateReactor(entity);
            else
                CreateTurbine(entity);

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

    private BoxContainer AddInstructions(FancyWindow window, string text)
    {
        // Preserve the machine's XAML hierarchy, named controls and event handlers.
        var root = (BoxContainer) window.ContentsContainer.Children.First();
        var panel = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        var label = new RichTextLabel();
        label.SetMessage(FormattedMessage.FromMarkupPermissive(text));
        panel.AddChild(label);
        root.AddChild(panel);
        panel.SetPositionInParent(0);
        return panel;
    }

    private ReactorSlotBUIData GetPart(EntProtoId id)
    {
        var proto = _prototypes.Index(id);
        if (!proto.TryGetComponent<ReactorPartComponent>(_factory.GetComponentName<ReactorPartComponent>(), out var part))
            throw new InvalidOperationException($"Reactor teaching part {id} has no ReactorPart component.");

        return new ReactorSlotBUIData
        {
            PartName = proto.Name,
            IconName = part.IconStateInserted,
            Temperature = Atmospherics.T20C,
        };
    }

    private void CreateReactor(EntityUid entity)
    {
        var window = new NuclearReactorWindow();
        _window = window;
        window.SetEntity(entity);
        window.Title = "Учебный реактор — пример, не подключён к кораблю";

        var component = _entities.GetComponent<NuclearReactorComponent>(entity);
        var prefab = _prototypes.Index<NuclearReactorPrefabPrototype>(component.Prefab);
        var state = new NuclearReactorBuiState
        {
            GridWidth = component.ReactorGridWidth,
            GridHeight = component.ReactorGridHeight,
            ReactorTemp = Atmospherics.T20C,
            ControlRodActual = 1,
            ControlRodSet = 1,
        };
        foreach (var (position, part) in prefab.ReactorComponents)
            state.SlotData.Add(position, GetPart(part));
        ReactorSlotBUIData? shelf = null;

        var instructions = AddInstructions(window,
            "[bold]Учебный пример: холодный реактор, управляющие стержни 50%.[/bold]\n" +
            "1. Слева внизу — полка; кнопка ниже кладёт на неё учебный стержень.\n" +
            "2. В центре — сетка 5×5. Пустые синие ячейки: строка/столбец 2/2, 2/4, 4/2, 4/4. Выберите ячейку, затем «ВСТАВИТЬ» слева.\n" +
            "3. Справа — управляющие стержни. «+ +» / «- -» меняют процент на 5. В этом примере движение мгновенное; в игре оно занимает время.");
        var load = new Button { Text = "Положить учебный топливный стержень на полку" };
        load.OnPressed += _ =>
        {
            shelf = GetPart("CerenkiteReactorFuelRod");
            state.ItemName = shelf.PartName;
            window.Update(state);
        };
        instructions.AddChild(load);

        window.ControlRodModify += change =>
        {
            state.ControlRodSet = Math.Clamp(state.ControlRodSet + change, 0, 2);
            state.ControlRodActual = state.ControlRodSet;
            window.Update(state);
        };
        window.ItemActionButtonPressed += position =>
        {
            var cell = new Vector2i((int) position.X, (int) position.Y);
            if (shelf != null && !state.SlotData.ContainsKey(cell))
            {
                state.SlotData.Add(cell, shelf);
                shelf = null;
                state.ItemName = null;
            }
            else if (shelf == null && state.SlotData.Remove(cell, out var part))
            {
                shelf = part;
                state.ItemName = part.PartName;
            }
            window.Update(state);
        };
        window.EjectButtonPressed += () =>
        {
            shelf = null;
            state.ItemName = null;
            window.Update(state);
        };
        window.Update(state);
    }

    private void CreateTurbine(EntityUid entity)
    {
        var window = new TurbineWindow();
        _window = window;
        window.SetEntity(entity);
        window.Title = "Учебная турбина — пример, не подключена к кораблю";
        var component = _entities.GetComponent<TurbineComponent>(entity);
        var state = new TurbineBuiState
        {
            RPM = component.BestRPM,
            BestRPM = component.BestRPM,
            FlowRateMin = 0,
            FlowRateMax = component.FlowRateMax,
            FlowRate = component.FlowRateMax,
            StatorLoadMin = 1000,
            StatorLoadMax = component.StatorLoadMax,
            StatorLoad = component.StatorLoad,
            Health = component.BladeHealthMax,
            HealthMax = component.BladeHealthMax,
        };
        AddInstructions(window,
            "[bold]Учебный пример: 600 об/мин. Симуляция нагрева и электросети не выполняется.[/bold]\n" +
            "1. «Расход» — верхнее поле справа: максимальное значение — 1000.\n" +
            "2. «Нагрузка статора» — нижнее поле. «+1000» тормозит настоящую турбину, «-1000» позволяет ей ускориться.\n" +
            "3. Вкладка «Компоненты»: «Целостность» и «Нагрузка» лопастей. При 600 об/мин нагрузка около 83%.\n" +
            "4. Слева от полей — шкала оборотов; выше полей — сигнальные лампы. Повреждения возможны выше 720 об/мин, а не при 90% нагрузки.");
        window.TurbineFlowRateChanged += value =>
        {
            state.FlowRate = Math.Clamp(value, state.FlowRateMin, state.FlowRateMax);
            window.Update(state);
        };
        window.TurbineStatorLoadChanged += value =>
        {
            state.StatorLoad = Math.Clamp(value, state.StatorLoadMin, state.StatorLoadMax);
            window.Update(state);
        };
        window.Update(state);
    }

    private void CloseExample()
    {
        var window = _window;
        _window = null;
        if (window != null)
        {
            window.OnClose -= CloseExample;
            window.Close();
            window.Dispose();
        }

        if (_dummy is { } entity && !_entities.Deleted(entity))
            _entities.DeleteEntity(entity);
        _dummy = null;
    }

    protected override void ExitedTree()
    {
        CloseExample();
        base.ExitedTree();
    }

    protected override void Dispose(bool disposing)
    {
        CloseExample();
        base.Dispose(disposing);
    }
}
