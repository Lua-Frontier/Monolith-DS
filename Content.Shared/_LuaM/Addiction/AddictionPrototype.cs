using Content.Shared._LuaM.Traits;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Addiction;

/// <summary>
/// An addiction a body picks up during the round by taking in too much of some chemicals in a short time.
/// Once picked up it works like the chemical dependency trait, but it can be cured.
/// </summary>
[Prototype]
public sealed partial class AddictionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// What the addiction is to, shown by the health analyzer and in popups.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Reagents that feed the addiction.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = new();

    /// <summary>
    /// Every reagent that inherits from one of these feeds the addiction too. Abstract parents work.
    /// </summary>
    [DataField]
    public List<string> ReagentParents = new();

    /// <summary>
    /// How much of the reagents has to be taken in within <see cref="Window"/> to pick up the addiction.
    /// </summary>
    [DataField]
    public FixedPoint2 Threshold = 75;

    [DataField]
    public TimeSpan Window = TimeSpan.FromMinutes(30);

    /// <summary>
    /// When set, the addiction is picked up only by an overdose: once the blood holds this much of the reagents at
    /// once. <see cref="Threshold"/> and <see cref="Window"/> are not used then.
    /// </summary>
    [DataField]
    public FixedPoint2? OverdoseThreshold;

    /// <summary>
    /// How much of the reagents one dose is once addicted.
    /// </summary>
    [DataField]
    public FixedPoint2 DoseAmount = 10;

    /// <summary>
    /// How long one dose holds withdrawal off.
    /// </summary>
    [DataField]
    public TimeSpan DoseDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How much of a treatment reagent cures the addiction.
    /// </summary>
    [DataField]
    public FixedPoint2 TreatmentAmount = 30;

    /// <summary>
    /// How long the body has to spend in withdrawal without a dose, over all its bouts, to get over the addiction on
    /// its own. A bout lasts two minutes, so the default is three bouts sat out.
    /// </summary>
    [DataField]
    public TimeSpan RecoveryTime = TimeSpan.FromMinutes(6);

    /// <summary>
    /// How much of the reagents taken in once addicted turns the addiction heavy: like the dependency from the trait,
    /// only <see cref="HeavyTreatmentAmount"/> of a treatment reagent cures it and sitting out withdrawal does not.
    /// Null for an addiction that never gets heavy.
    /// </summary>
    [DataField]
    public FixedPoint2? HeavyThreshold = 400;

    [DataField]
    public FixedPoint2 HeavyTreatmentAmount = 150;

    /// <summary>
    /// What withdrawal from this addiction does. The usual shaking, falling and groaning when not set.
    /// </summary>
    [DataField]
    public ChemicalWithdrawalEffects? Withdrawal;
}
