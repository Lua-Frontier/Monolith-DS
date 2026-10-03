using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Content.Shared._LuaM.Procedural.GridAssembly;

namespace Content.Server._LuaM.TheCircle.FrontierWar;

/// <summary>
/// Configures the staged arrival of the Circle and its generated battlefield.
/// Timing, entity composition, and grid assembly are selected through prototypes.
/// </summary>
[RegisterComponent]
public sealed partial class CircleFrontierWarComponent : Component
{
    [DataField]
    public TimeSpan CircleArrivalDelay = TimeSpan.FromHours(1);

    [DataField]
    public TimeSpan BattlefieldPreparationTime = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan BattlefieldRevealDelay = TimeSpan.FromMinutes(45);

    [DataField]
    public TimeSpan ForcedAwakeningDelay = TimeSpan.FromHours(1);

    [DataField]
    public int StartingReinforcements = 1500;

    [DataField]
    public int CapturePointDrain = 5;

    [DataField]
    public TimeSpan CapturePointDrainInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public int KillDrain = 1;

    [DataField]
    public TimeSpan RespawnDelay = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan CollapseInterval = TimeSpan.FromMinutes(7);

    [DataField]
    public float DreadnoughtThreshold = 0.4f;

    [DataField]
    public TimeSpan DreadnoughtLifetime = TimeSpan.FromMinutes(9);

    [DataField]
    public float BattlefieldRadius = 80f;

    [DataField]
    public HashSet<EntProtoId> ForbiddenEquipment = new();

    [DataField]
    public EntProtoId CircleAnchor = "CircleFrontierWarCircleAnchor";

    [DataField]
    public EntProtoId CollapseRock = "CircleFrontierWarCollapsibleBasalt";

    [DataField]
    public ProtoId<StartingGearPrototype> DreadnoughtGear = "CircleFrontierWarDreadnoughtGear";

    [DataField]
    public EntProtoId DreadnoughtOfferAction = "ActionCircleFrontierWarAcceptDreadnought";

    [DataField]
    public EntProtoId NavigationAction = "ActionCircleFrontierWarToggleNavigation";

    [DataField]
    public SoundSpecifier BattlefieldRevealSound = new SoundPathSpecifier("/Audio/_LuaM/Necromorfs/Obelisk2.ogg");

    [DataField]
    public ProtoId<GridAssemblyPrototype> BattlefieldAssembly = "CircleFrontierWarVgroidAssembly";

    [DataField]
    public Dictionary<EntProtoId, int> CircleGhostRoles = new()
    {
        { "SpawnPointCircleCommander", 1 },
        { "SpawnPointCircleEngineer", 2 },
        { "SpawnPointCircleGeist", 1 },
        { "SpawnPointCircleFighterLight", 4 },
        { "SpawnPointCircleFighterHeavy", 4 },
        { "SpawnPointCircleMedic", 2 },
    };

    public TimeSpan NextStageTime;

    public CircleFrontierWarStage Stage;

    public int CrewReinforcements;

    public int CircleReinforcements;

    public TimeSpan NextScoreTick;

    public float CaptureAccumulator;

    public Dictionary<EntityUid, TimeSpan> RespawnQueue = new();

    public List<EntityCoordinates> DugRockPositions = new();

    public TimeSpan ForcedAwakeningAt;

    public TimeSpan NextCollapseAt;

    public bool ForcedAwakeningTriggered;

    public bool ObeliskDestroyed;

    public HashSet<CircleFrontierWarSide> DreadnoughtOffered = new();

    public Dictionary<CircleFrontierWarSide, EntityUid> ActiveDreadnoughts = new();

    public Dictionary<CircleFrontierWarSide, int> DreadnoughtSavedScores = new();

    public bool BattlefieldGenerationStarted;

    public bool BattlefieldReady;

    public EntityUid? BattlefieldGrid;

    public int MergedModuleCount;
}

public enum CircleFrontierWarStage : byte
{
    WaitingForCircle,
    Infiltration,
    BattlefieldHidden,
    BattlefieldRevealed,
    Finished,
}

/// <summary>
/// Map marker where the initial Circle ghost-role spawners are created.
/// </summary>
[RegisterComponent]
public sealed partial class CircleFrontierWarCircleAnchorComponent : Component;

/// <summary>
/// Temporary marker representing the future composite battlefield grid.
/// </summary>
[RegisterComponent]
public sealed partial class CircleFrontierWarBattlefieldAnchorComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarCrewSpawnComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarCircleSpawnComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarObeliskComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarCollapsibleRockComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarGateComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarParticipantComponent : Component
{
    [DataField]
    public CircleFrontierWarSide Side;

    public EntityUid? NavigationActionEntity;

    public EntityUid? DreadnoughtOfferActionEntity;
}

[RegisterComponent]
public sealed partial class CircleFrontierWarDreadnoughtComponent : Component
{
    public CircleFrontierWarSide Side;
    public TimeSpan DiesAt;
}

/// <summary>
/// Protects a battlefield casualty until the rule rejuvenates the same body.
/// Keeping the entity preserves the exact equipment and its internal state.
/// </summary>
[RegisterComponent]
public sealed partial class CircleFrontierWarPendingRespawnComponent : Component;

[RegisterComponent]
public sealed partial class CircleFrontierWarCapturePointComponent : Component
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public float Radius = 4f;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(20);

    public CircleFrontierWarSide ControllingSide;
    public CircleFrontierWarSide CapturingSide;
    public TimeSpan Progress;
}

public enum CircleFrontierWarSide : byte
{
    None,
    Crew,
    Circle,
}
