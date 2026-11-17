using Robust.Shared.GameStates;

namespace Content.Shared.Station.Components;

/// <summary>
/// Stores core information about a station, namely its config and associated grids.
/// All station entities will have this component.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedStationSystem), typeof(Content.Shared._LuaM.Performance.DeletedRefStateSystem))] // LuaM
public sealed partial class StationDataComponent : Component
{
    /// <summary>
    /// The game map prototype, if any, associated with this station.
    /// </summary>
    [DataField]
    public StationConfig? StationConfig;

    /// <summary>
    /// List of all grids this station is part of.
    /// </summary>
    [DataField] // LuaM
    public HashSet<EntityUid> Grids = new();
}
