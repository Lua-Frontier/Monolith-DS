using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Procedural;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._LuaM.Procedural.GridAssembly;

/// <summary>
/// Describes a procedural base grid and the temporary module grids that will be merged into it.
/// </summary>
[Prototype]
public sealed partial class GridAssemblyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Optional procedural base. When omitted, the final grid is assembled entirely from modules.
    /// </summary>
    [DataField]
    public ProtoId<DungeonConfigPrototype>? Dungeon;

    [DataField]
    public Vector2 WorldOffset;

    [DataField]
    public ProtoId<ContentTileDefinition> ModuleFloorTile = "FloorSteel";

    [DataField]
    public ProtoId<ContentTileDefinition> ConnectionFloorTile = "FloorSteel";

    [DataField]
    public int PlacementAttempts = 500;

    [DataField]
    public float ClearPadding = 1f;

    [DataField]
    public HashSet<EntProtoId> ClearEntityPrototypes = [];

    [DataField(required: true)]
    public List<GridAssemblyModule> Modules = [];

    [DataField]
    public List<GridAssemblyConnection> Connections = [];
}

[DataDefinition]
public sealed partial class GridAssemblyModule
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// Half-size of a generated rectangular module. When <see cref="GridPath"/> is set, this is only used
    /// as the configurable clearing area around the loaded grid's origin.
    /// </summary>
    [DataField]
    public Vector2i? HalfSize;

    /// <summary>
    /// Optional saved single-grid YAML. When set, the loaded grid and all of its entities are merged into the result.
    /// </summary>
    [DataField]
    public ResPath? GridPath;

    /// <summary>
    /// Fixed local offset. Ignored when <see cref="RandomPlacement"/> is configured.
    /// </summary>
    [DataField]
    public Vector2 Offset;

    [DataField]
    public Angle Rotation;

    [DataField]
    public GridAssemblyRadialPlacement? RandomPlacement;

    [DataField]
    public ProtoId<ContentTileDefinition>? FloorTile;

    [DataField]
    public List<GridAssemblyEntitySpawn> Entities = [];
}

[DataDefinition]
public sealed partial class GridAssemblyRadialPlacement
{
    [DataField]
    public string Origin = string.Empty;

    [DataField]
    public float MinimumRadius;

    [DataField(required: true)]
    public float MaximumRadius;

    /// <summary>
    /// Minimum distance to already placed modules, keyed by module ID.
    /// </summary>
    [DataField]
    public Dictionary<string, float> MinimumDistances = [];
}

[DataDefinition]
public sealed partial class GridAssemblyConnection
{
    [DataField(required: true)]
    public string From = string.Empty;

    [DataField(required: true)]
    public string To = string.Empty;

    [DataField]
    public int Width = 1;

    [DataField]
    public ProtoId<ContentTileDefinition>? FloorTile;
}

[DataDefinition]
public sealed partial class GridAssemblyEntitySpawn
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public Vector2 Offset;

    [DataField]
    public bool GlobalPvs;
}
