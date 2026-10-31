using System.Linq;
using System.Numerics;
using Content.Client.Mind;
using Content.Shared._Mono.Shipyard;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Anomaly;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Client._Mono.Shipyard;

/// <summary>
/// This handles spawning client-side grid and getting data from it.
/// </summary>
public sealed partial class ShipyardPreviewSystem : SharedShipyardPreviewSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private MindSystem _mind = default!;

    public Entity<MapGridComponent>? CurrentGrid;

    // LuaM start: the preview map may reach the client after the preview button was pressed
    // (PVS states are sent asynchronously since RT 291), the vessel is loaded once the map arrives
    private VesselPrototype? _pendingVessel;

    /// <summary>
    /// Raised when the previewed grid got loaded, so an already open preview window can show its data.
    /// </summary>
    public event Action? GridLoaded;
    // LuaM end

    public override void Initialize()
    {
        base.Initialize();
    }

    // LuaM start
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_pendingVessel is not { } vessel)
            return;

        CachePreviewMap();
        if (_previewMap == MapId.Nullspace || !_map.MapExists(_previewMap))
            return;

        _pendingVessel = null;
        LoadGrid(vessel);
    }
    // LuaM end

    public bool TryPreviewGrid(VesselPrototype vessel)
    {
        CachePreviewMap();

        // LuaM start
        _pendingVessel = null;
        if (_previewMap == MapId.Nullspace || !_map.MapExists(_previewMap))
        {
            _pendingVessel = vessel;
            return false;
        }

        return LoadGrid(vessel);
    }

    private bool LoadGrid(VesselPrototype vessel)
    {
        CachePreviewMap();
        if (_previewMap == MapId.Nullspace)
            return false;
        // LuaM end

        var opts = new DeserializationOptions();
        if (!_loader.TryLoadGrid(_previewMap,
                vessel.ShuttlePath,
                out var grid,
                opts))
            return false;

        _xform.SetMapCoordinates(grid.Value, new MapCoordinates(Vector2.Zero, _previewMap));
        _meta.SetEntityName(grid.Value, vessel.Name);
        CurrentGrid = grid.Value;
        GridLoaded?.Invoke(); // LuaM
        return true;
    }

    public FormattedMessage GetGridData()
    {
        var msg = new FormattedMessage();
        if (CurrentGrid == null)
            return msg;

        msg.AddMarkupOrThrow(
            Loc.GetString("shipyard-preview-tile-count", ("count", _map.GetAllTiles(CurrentGrid.Value.Owner, CurrentGrid.Value.Comp).Count().ToString()))
            );

        return msg;
    }

    public void Dispose()
    {
        _pendingVessel = null; // LuaM
        Del(CurrentGrid);
        CurrentGrid = null;
    }
}

