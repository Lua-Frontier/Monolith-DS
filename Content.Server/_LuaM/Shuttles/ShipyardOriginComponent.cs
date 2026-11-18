using Content.Shared._NF.Shipyard;

namespace Content.Server._LuaM.Shuttles;

/// <summary>
/// Remembers which shipyard listing this ship belongs to, e.g. to tell Vanguard ships from the others.
/// Bought ships get it from their vessel prototype, other faction ships from their own prototype.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardOriginComponent : Component
{
    [DataField]
    public ShipyardConsoleUiKey Group = ShipyardConsoleUiKey.Shipyard;
}
