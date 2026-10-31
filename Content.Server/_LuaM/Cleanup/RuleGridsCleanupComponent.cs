namespace Content.Server._LuaM.Cleanup;

/// <summary>
/// Put on a game rule that loads grids (LoadMapRule): its grids get these grid cleanup settings,
/// and so do the pieces they break into, see <see cref="GridCleanupInheritSystem"/>.
/// The fields are the same as in <see cref="Content.Server._Mono.Cleanup.GridCleanupGridComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class RuleGridsCleanupComponent : Component
{
    /// <summary>
    /// How many times faster the grids get cleaned up than mono.grid_cleanup_duration.
    /// </summary>
    [DataField]
    public float CleanupAcceleration = 1f;

    /// <summary>
    /// If set, the grids get cleaned up when no player is in this range instead of the default one.
    /// </summary>
    [DataField]
    public float? DistanceOverride;

    /// <summary>
    /// Clean up the grids even if they have a powered APC.
    /// </summary>
    [DataField]
    public bool IgnorePowered;

    /// <summary>
    /// Clean up the grids even if they are expensive.
    /// </summary>
    [DataField]
    public bool IgnorePrice;

    /// <summary>
    /// Clean up the grids even if their IFF label is shown.
    /// </summary>
    [DataField]
    public bool IgnoreIFF;
}
