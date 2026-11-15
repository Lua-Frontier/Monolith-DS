using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Audio;

namespace Content.Server._LuaM.Nebula;

/// <summary>
/// Время от времени переносит погоду опасных туманностей на безвредные: прежние затихают, новые становятся опасными,
/// число опасных туманностей при этом не меняется.
/// </summary>
[RegisterComponent, Access(typeof(NebulaShiftRuleSystem))]
public sealed partial class NebulaShiftRuleComponent : Component
{
    /// <summary>
    /// Через сколько секунд после начала раунда туманности сместятся впервые.
    /// </summary>
    [DataField]
    public MinMax FirstDelay = new(2400, 3600);

    /// <summary>
    /// Сколько секунд между смещениями.
    /// </summary>
    [DataField]
    public MinMax Interval = new(2400, 3600);

    /// <summary>
    /// Какая доля опасных туманностей переносит погоду за одно смещение.
    /// </summary>
    [DataField]
    public float Fraction = 1f;

    /// <summary>
    /// Насколько от края новой опасной туманности должны быть станции, POI и прочие гриды, кроме кораблей игроков.
    /// </summary>
    [DataField]
    public float GridClearance = 500f;

    /// <summary>
    /// Насколько от края новой опасной туманности должны быть корабли игроков, чтобы погода не накрыла их внезапно.
    /// </summary>
    [DataField]
    public float ShipClearance = 150f;

    [DataField]
    public LocId? Announcement = "luam-nebula-shift-announcement";

    [DataField]
    public LocId AnnouncementSender = "luam-nebula-shift-sender";

    [DataField]
    public SoundSpecifier? AnnouncementSound;

    [DataField]
    public Color AnnouncementColor = Color.FromHex("#5AD0FF");

    [ViewVariables]
    public TimeSpan? NextShift;
}
