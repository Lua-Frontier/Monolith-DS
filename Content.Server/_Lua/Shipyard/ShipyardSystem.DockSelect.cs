// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lua.Shipyard.BUIStates;
using Content.Shared._Lua.Shipyard.Components;
using Content.Shared._Lua.Shipyard.Events;
using Content.Shared._NF.Shipyard.BUI;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem
{
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;

    /// <summary>
    /// Extra space around the console grid shown on the dock selection radar.
    /// </summary>
    private const float DockRadarPadding = 5f;

    private void InitializeDockSelect()
    {
        SubscribeLocalEvent<ShipyardConsoleComponent, SelectDockPortMessage>(OnSelectDockPort);
    }

    private void OnSelectDockPort(EntityUid uid, ShipyardConsoleComponent component, SelectDockPortMessage args)
    {
        var lua = EnsureComp<ShipyardLuaConsoleComponent>(uid);
        lua.SelectedDockPort = IsSelectableDock(uid, args.SelectedDockPort, out _) ? args.SelectedDockPort : null;
        Dirty(uid, lua);

        if (TryGetShipyardState(uid, args.UiKey, out var baseState))
            _ui.SetUiState(uid, args.UiKey, ExtendUiStateLua(uid, baseState));
    }

    /// <summary>
    /// Gets the current shipyard console state, unwrapping the dock selection state if needed.
    /// </summary>
    private bool TryGetShipyardState(EntityUid uid, Enum uiKey, [NotNullWhen(true)] out ShipyardConsoleInterfaceState? state)
    {
        state = null;
        if (!_ui.TryGetUiState<BoundUserInterfaceState>(uid, uiKey, out var current))
            return false;

        state = current switch
        {
            ShipyardConsoleLuaDockSelectState dockState => dockState.BaseState,
            ShipyardConsoleInterfaceState plainState => plainState,
            _ => null,
        };
        return state != null;
    }

    /// <summary>
    /// Checks that the dock belongs to the same grid as the console.
    /// </summary>
    private bool IsSelectableDock(EntityUid consoleUid, NetEntity? netDock, [NotNullWhen(true)] out EntityUid? dockUid)
    {
        dockUid = null;
        if (netDock is not { } net
            || !TryGetEntity(net, out var dock)
            || !HasComp<DockingComponent>(dock))
            return false;

        var consoleGrid = Transform(consoleUid).GridUid;
        if (consoleGrid == null || Transform(dock.Value).GridUid != consoleGrid)
            return false;

        dockUid = dock;
        return true;
    }

    /// <summary>
    /// Wraps the shipyard console state with the radar data used to pick a docking port.
    /// </summary>
    private BoundUserInterfaceState ExtendUiStateLua(EntityUid uid, ShipyardConsoleInterfaceState state)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { Valid: true } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return state;

        var gridNet = GetNetEntity(gridUid);
        var dockDict = new Dictionary<NetEntity, List<DockingPortState>>();
        if (_shuttleConsole.GetAllDocks().TryGetValue(gridNet, out var ports))
            dockDict[gridNet] = ports;

        var bounds = gridComp.LocalAABB;
        var radius = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * 0.5f + DockRadarPadding;
        var nav = new NavInterfaceState(radius,
            new NetCoordinates(gridNet, bounds.Center),
            Angle.Zero,
            dockDict,
            InertiaDampeningMode.Dampen,
            true,
            null,
            null,
            null,
            true);

        TryComp(uid, out ShipyardLuaConsoleComponent? lua);
        return new ShipyardConsoleLuaDockSelectState(state, nav, lua?.SelectedDockPort);
    }

    /// <summary>
    /// Purchases a shuttle and docks it to the port selected on the console.
    /// Falls back to the regular station docking if no port is selected or the shuttle doesn't fit there.
    /// </summary>
    public bool TryPurchaseShuttleToDock(EntityUid consoleUid, EntityUid stationUid, ResPath shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;
        if (TryComp<ShipyardLuaConsoleComponent>(consoleUid, out var lua)
            && IsSelectableDock(consoleUid, lua.SelectedDockPort, out var dockUid)
            && TryComp<DockingComponent>(dockUid, out var dockComp)
            && !dockComp.Docked
            && Transform(dockUid.Value).GridUid is { } targetGrid
            && HasComp<StationDataComponent>(stationUid))
        {
            if (TryAddShuttle(shuttlePath, out var shuttleGrid))
            {
                var config = HasComp<ShuttleComponent>(shuttleGrid)
                    ? _docking.GetDockingConfigForGridDock(shuttleGrid.Value, targetGrid, dockUid.Value, dockType: dockComp.DockType)
                    : null;

                if (config != null)
                {
                    _sawmill.Info($"Shuttle {shuttlePath} was purchased at {ToPrettyString(stationUid)} and docked to {ToPrettyString(dockUid.Value)}");
                    var ev = new ShipBoughtEvent();
                    RaiseLocalEvent(shuttleGrid.Value, ev);
                    _shuttle.FTLDock((shuttleGrid.Value, Transform(shuttleGrid.Value)), config);
                    shuttleEntityUid = shuttleGrid;
                    return true;
                }

                QueueDel(shuttleGrid);
            }
        }

        return TryPurchaseShuttle(stationUid, shuttlePath, out shuttleEntityUid);
    }
}
