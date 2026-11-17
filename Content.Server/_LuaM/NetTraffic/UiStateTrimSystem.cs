using Content.Server.Ame.Components;
using Content.Server.Ame.EntitySystems;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Server.Holopad;
using Content.Shared.Ame.Components;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Holopad;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;

namespace Content.Server._LuaM.NetTraffic;

// -- Движок шлёт сохранённое UI-состояние всем, кто видит сущность, даже при закрытом окне; держим его только пока окно открыто.
public sealed class UiStateTrimSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private GasCanisterSystem _canister = default!;
    [Dependency] private HolopadSystem _holopad = default!;
    [Dependency] private AmeControllerSystem _ame = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadarConsoleComponent, BoundUIClosedEvent>(OnRadarClosed);
        SubscribeLocalEvent<GasCanisterComponent, BoundUIOpenedEvent>(OnCanisterOpened);
        SubscribeLocalEvent<HolopadComponent, BoundUIOpenedEvent>(OnHolopadOpened);
        SubscribeLocalEvent<AmeControllerComponent, BoundUIOpenedEvent>(OnAmeOpened);
    }

    private void OnRadarClosed(EntityUid uid, RadarConsoleComponent component, BoundUIClosedEvent args)
    {
        if (args.UiKey is RadarConsoleUiKey.Key && !_ui.IsUiOpen(uid, RadarConsoleUiKey.Key))
            _ui.SetUiState(uid, RadarConsoleUiKey.Key, null);
    }

    private void OnCanisterOpened(EntityUid uid, GasCanisterComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is GasCanisterUiKey.Key)
            _canister.RefreshUi(uid, component);
    }

    private void OnHolopadOpened(EntityUid uid, HolopadComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is HolopadUiKey.InteractionWindow or HolopadUiKey.AiActionWindow)
            _holopad.UpdateUIState((uid, component));
    }

    private void OnAmeOpened(EntityUid uid, AmeControllerComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is AmeControllerUiKey.Key)
            _ame.UpdateUi(uid, component);
    }
}
