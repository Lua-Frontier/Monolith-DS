using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Shuttles;

/// <summary>
/// Marks the grids of the sector station and of points of interest, so the FTL destination list
/// can put them right under the player's own ship.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SectorLandmarkComponent : Component;
