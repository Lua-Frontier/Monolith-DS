using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._LuaM.Shuttles;

namespace Content.Server._LuaM.Shuttles;

/// <summary>
/// Marks the grids of the round's own map, the sector station. Points of interest are marked where they spawn.
/// </summary>
public sealed partial class SectorLandmarkSystem : EntitySystem
{
    [Dependency] private IGameMapManager _gameMapManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PostGameMapLoad>(OnGameMapLoad);
    }

    private void OnGameMapLoad(PostGameMapLoad ev)
    {
        // Game rules load their maps the same way (LoadMapRule), those stay below the points of interest.
        if (ev.GameMap.ID != _gameMapManager.GetSelectedMap()?.ID)
            return;

        foreach (var grid in ev.Grids)
        {
            EnsureComp<SectorLandmarkComponent>(grid);
        }
    }
}
