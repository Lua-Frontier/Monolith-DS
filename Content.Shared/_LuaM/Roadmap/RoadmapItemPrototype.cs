using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Roadmap;

/// <summary>
/// One entry of the roadmap window in the lobby and the game menu.
/// </summary>
[Prototype]
public sealed partial class RoadmapItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public RoadmapStage Stage;

    /// <summary>
    /// Lower values go higher in the column.
    /// </summary>
    [DataField]
    public int Order;
}

public enum RoadmapStage : byte
{
    Near,
    Later,
    Done,
}
