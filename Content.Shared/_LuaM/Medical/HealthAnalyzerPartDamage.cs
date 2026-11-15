using Content.Shared._Shitmed.Targeting;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Medical;

/// <summary>
/// Damage of one body part, sent with the health analyzer scan so every limb is listed without clicking it.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerPartDamage
{
    public TargetBodyPart Part;
    public Dictionary<string, FixedPoint2> Damage;

    public HealthAnalyzerPartDamage(TargetBodyPart part, Dictionary<string, FixedPoint2> damage)
    {
        Part = part;
        Damage = damage;
    }
}
