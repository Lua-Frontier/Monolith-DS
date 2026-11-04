using Robust.Shared.Audio;

namespace Content.Server._LuaM.SectorAnomaly;

public abstract partial class SectorAnomalyRuleComponent : Component
{
    [DataField]
    public SoundSpecifier? Music;

    [DataField]
    public float MessageChancePerSecond;

    [DataField]
    public List<LocId> Messages = new();

    [ViewVariables]
    public EntityUid? Map;

    [ViewVariables]
    public float EffectTimer;

    [ViewVariables]
    public bool Blocked;
}
