using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Server._LuaM.SectorAnomaly;

[RegisterComponent, Access(typeof(SectorAnomalySchedulerSystem))]
public sealed partial class SectorAnomalySchedulerComponent : Component
{
    [DataField]
    public float Chance = 0.3f;

    [DataField]
    public MinMax StartTime = new(3600, 21600);

    [DataField]
    public TimeSpan RetryDelay = TimeSpan.FromMinutes(10);

    [DataField(required: true)]
    public EntityTableSelector Anomalies = default!;

    [ViewVariables]
    public TimeSpan? NextAttempt;

    [ViewVariables]
    public TimeSpan Deadline;
}
