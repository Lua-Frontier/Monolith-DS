using Content.Server.Shuttles.Systems;
using Content.Shared._NF.Shipyard; // LuaM
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Components;

[RegisterComponent, Access(typeof(ShuttleSystem))]
public sealed partial class IFFConsoleComponent : Component
{
    /// <summary>
    /// Flags that this console is allowed to set.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("allowedFlags")]
    public IFFFlags AllowedFlags = IFFFlags.HideLabel;

    /// <summary>
    /// LuaM: shipyard listings of the ships this console works on. Empty means any grid.
    /// </summary>
    [DataField]
    public List<ShipyardConsoleUiKey> AllowedShipGroups = new();
}
