using Content.Shared._Mono.Company;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Shuttles;

/// <summary>
/// Lets faction members see their faction's shuttles on radar even when those shuttles have their IFF turned off.
/// Factions are matched by <see cref="CompanyPrototype.FriendlyIffGroup"/> of the viewer's and the grid's company,
/// or by <see cref="FriendlyIffComponent"/> on the grid.
/// </summary>
public sealed partial class FriendlyIffSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    /// <summary>
    /// Whether the grid belongs to the same friendly IFF group as the viewer.
    /// </summary>
    public bool IsFriendly(EntityUid? viewer, EntityUid grid)
    {
        if (viewer == null || !TryComp<CompanyComponent>(viewer, out var viewerCompany))
            return false;

        var group = GetGroup(viewerCompany.CompanyName);
        return group != null && group == GetGridGroup(grid);
    }

    /// <summary>
    /// <see cref="IsFriendly"/> for the locally controlled entity. Always false on the server.
    /// </summary>
    public bool IsFriendlyToLocalPlayer(EntityUid grid)
    {
        return IsFriendly(_player.LocalEntity, grid);
    }

    /// <summary>
    /// Whether the grid is friendly to the local player and must be shown to them at any range,
    /// see <see cref="FriendlyIffComponent.AlwaysVisible"/>.
    /// </summary>
    public bool IsAlwaysVisibleToLocalPlayer(EntityUid grid)
    {
        return TryComp<FriendlyIffComponent>(grid, out var friendly)
            && friendly.AlwaysVisible
            && IsFriendlyToLocalPlayer(grid);
    }

    private string? GetGridGroup(EntityUid grid)
    {
        if (TryComp<FriendlyIffComponent>(grid, out var friendly))
            return friendly.Group;

        return TryComp<CompanyComponent>(grid, out var company) ? GetGroup(company.CompanyName) : null;
    }

    private string? GetGroup(ProtoId<CompanyPrototype> company)
    {
        return _proto.TryIndex(company, out var proto) ? proto.FriendlyIffGroup : null;
    }
}
