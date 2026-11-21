using Content.Server.Chemistry.Components;
using Content.Server.Construction;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Placeable;
using Content.Shared.Power;
// LuaM-start
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
// LuaM-end

namespace Content.Server.Chemistry.EntitySystems;

public sealed partial class SolutionHeaterSystem : EntitySystem
{
    [Dependency] private PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!; // LuaM

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionHeaterComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SolutionHeaterComponent, RefreshPartsEvent>(OnRefreshParts);
        SubscribeLocalEvent<SolutionHeaterComponent, UpgradeExamineEvent>(OnUpgradeExamine);
        SubscribeLocalEvent<SolutionHeaterComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<SolutionHeaterComponent, ItemRemovedEvent>(OnItemRemoved);
        // LuaM-start
        SubscribeLocalEvent<SolutionHeaterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SolutionHeaterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<SolutionHeaterComponent, ExaminedEvent>(OnExamined);
        // LuaM-end
    }

    // LuaM-start
    private void OnStartup(Entity<SolutionHeaterComponent> entity, ref ComponentStartup args)
    {
        _appearance.SetData(entity, SolutionHeaterVisuals.IsCooling, entity.Comp.Cooling);
    }

    private void OnGetAltVerbs(Entity<SolutionHeaterComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(entity.Comp.Cooling
                ? "solution-heater-verb-set-heating"
                : "solution-heater-verb-set-cooling"),
            Act = () => SetCooling(entity, !entity.Comp.Cooling, user),
        });
    }

    private void OnExamined(Entity<SolutionHeaterComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(entity.Comp.Cooling
            ? "solution-heater-examine-cooling"
            : "solution-heater-examine-heating"));
    }

    public void SetCooling(Entity<SolutionHeaterComponent> entity, bool cooling, EntityUid? user = null)
    {
        if (entity.Comp.Cooling == cooling)
            return;

        entity.Comp.Cooling = cooling;
        _appearance.SetData(entity, SolutionHeaterVisuals.IsCooling, cooling);

        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString(cooling
                ? "solution-heater-popup-cooling"
                : "solution-heater-popup-heating"), entity, user.Value);
        }
    }
    // LuaM-end

    private void TurnOn(EntityUid uid)
    {
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, true);
        EnsureComp<ActiveSolutionHeaterComponent>(uid);
    }

    public bool TryTurnOn(EntityUid uid, ItemPlacerComponent? placer = null)
    {
        if (!Resolve(uid, ref placer))
            return false;

        if (placer.PlacedEntities.Count <= 0 || !_powerReceiver.IsPowered(uid))
            return false;

        TurnOn(uid);
        return true;
    }

    public void TurnOff(EntityUid uid)
    {
        _appearance.SetData(uid, SolutionHeaterVisuals.IsOn, false);
        RemComp<ActiveSolutionHeaterComponent>(uid);
    }

    private void OnPowerChanged(Entity<SolutionHeaterComponent> entity, ref PowerChangedEvent args)
    {
        var placer = Comp<ItemPlacerComponent>(entity);
        if (args.Powered && placer.PlacedEntities.Count > 0)
        {
            TurnOn(entity);
        }
        else
        {
            TurnOff(entity);
        }
    }

    private void OnRefreshParts(Entity<SolutionHeaterComponent> entity, ref RefreshPartsEvent args)
    {
        var heatRating = args.PartRatings[entity.Comp.MachinePartHeatMultiplier] - 1;

        entity.Comp.HeatPerSecond = entity.Comp.BaseHeatPerSecond * MathF.Pow(entity.Comp.PartRatingHeatMultiplier, heatRating);
    }

    private void OnUpgradeExamine(Entity<SolutionHeaterComponent> entity, ref UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("solution-heater-upgrade-heat", entity.Comp.HeatPerSecond / entity.Comp.BaseHeatPerSecond);
    }

    private void OnItemPlaced(Entity<SolutionHeaterComponent> entity, ref ItemPlacedEvent args)
    {
        TryTurnOn(entity);
    }

    private void OnItemRemoved(Entity<SolutionHeaterComponent> entity, ref ItemRemovedEvent args)
    {
        var placer = Comp<ItemPlacerComponent>(entity);
        if (placer.PlacedEntities.Count == 0) // Last entity was removed
            TurnOff(entity);
    }

    // LuaM-start
    private const float HeatStep = 0.5f;
    private float _heatTime;
    // LuaM-end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // LuaM-start
        _heatTime += frameTime;
        if (_heatTime < HeatStep)
            return;

        frameTime = _heatTime;
        _heatTime = 0f;
        // LuaM-end

        var query = EntityQueryEnumerator<ActiveSolutionHeaterComponent, SolutionHeaterComponent, ItemPlacerComponent>();
        while (query.MoveNext(out _, out _, out var heater, out var placer))
        {
            foreach (var heatingEntity in placer.PlacedEntities)
            {
                if (!TryComp<SolutionContainerManagerComponent>(heatingEntity, out var container))
                    continue;

                var energy = heater.HeatPerSecond * frameTime;
                foreach (var (_, soln) in _solutionContainer.EnumerateSolutions((heatingEntity, container)))
                {
                    // LuaM-start
                    if (heater.Cooling)
                    {
                        if (soln.Comp.Solution.Temperature <= heater.MinTemperature)
                            continue;

                        _solutionContainer.AddThermalEnergy(soln, -energy);
                        if (soln.Comp.Solution.Temperature < heater.MinTemperature)
                            _solutionContainer.SetTemperature(soln, heater.MinTemperature);
                        continue;
                    }
                    // LuaM-end

                    _solutionContainer.AddThermalEnergy(soln, energy);
                }
            }
        }
    }
}
