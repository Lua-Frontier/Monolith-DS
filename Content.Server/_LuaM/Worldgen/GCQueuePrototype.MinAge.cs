namespace Content.Server.Worldgen.Prototypes;

public sealed partial class GCQueuePrototype
{
    [DataField("minAge")]
    public TimeSpan MinAge { get; private set; } = TimeSpan.Zero;
}
