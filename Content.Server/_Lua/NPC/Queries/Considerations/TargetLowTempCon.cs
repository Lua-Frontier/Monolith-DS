using Content.Server.NPC.Queries.Considerations;

namespace Content.Server._Lua.NPC.Queries.Considerations;

/// <summary>
/// Lua: returns if the target is below a certain temperature. Taken from sector-frontier-14 (wizden #37135), used by the basilisk.
/// </summary>
public sealed partial class TargetLowTempCon : UtilityConsideration
{
    /// <summary>
    /// The minimum temperature they must be.
    /// </summary>
    [DataField]
    public float MinTemp;
}
