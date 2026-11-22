using Content.Shared._LuaM.Addiction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Dependencies of a body on chemicals. The trait gives one on a chemical picked at random when the body spawns;
/// the round adds addictions on top of it. Every dose of a chemical holds its withdrawal off for a while.
/// In withdrawal the body shakes, falls, drops what it holds and makes noises.
/// </summary>
[RegisterComponent]
public sealed partial class ChemicalDependencyComponent : Component
{
    /// <summary>
    /// Whether the body picks a random chemical to depend on when it spawns. Off for bodies that only picked up
    /// an addiction during the round.
    /// </summary>
    [DataField]
    public bool PickRandom = true;

    /// <summary>
    /// Reagent groups the dependency is picked from. Only reagents that some reaction makes are picked.
    /// </summary>
    [DataField]
    public List<string> Groups = new() { "Medicine", "Narcotics", "Toxins" };

    /// <summary>
    /// Reagents that are never picked.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> Blacklist = new();

    /// <summary>
    /// The chemical the body depends on from spawn. Picked at random on spawn when not set.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>
    /// How much of the chemical has to be taken in for one dose.
    /// </summary>
    [DataField]
    public FixedPoint2 DoseAmount = 10;

    /// <summary>
    /// How long one dose holds withdrawal off.
    /// </summary>
    [DataField]
    public TimeSpan DoseDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How much of a treatment reagent cures the dependency from spawn. Far more than an addiction picked up in the round.
    /// </summary>
    [DataField]
    public FixedPoint2 TreatmentAmount = 150;

    /// <summary>
    /// How long before withdrawal the body starts to crave the chemical.
    /// </summary>
    [DataField]
    public TimeSpan WarningTime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How much of the treatment progress, from 0 to 1, one full dose of the chemical takes back. Smaller amounts take
    /// back their share of it.
    /// </summary>
    [DataField]
    public float RelapsePerDose = 0.25f;

    /// <summary>
    /// How long a bout of withdrawal lasts without a dose. Sat out, it lets go until the next bout, one dose duration later.
    /// </summary>
    [DataField]
    public TimeSpan WithdrawalDuration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How often withdrawal rolls its effects.
    /// </summary>
    [DataField]
    public TimeSpan EffectInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// What withdrawal does, unless the addiction in withdrawal has effects of its own.
    /// </summary>
    [DataField]
    public ChemicalWithdrawalEffects Withdrawal = new();

    /// <summary>
    /// Every chemical the body depends on right now.
    /// </summary>
    [ViewVariables]
    public List<ChemicalDependency> Dependencies = new();

    /// <summary>
    /// Paused time is added by the system, together with the times of every dependency.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextEffect;

    /// <summary>
    /// The relapse popup shows at most this often, the chemical is taken in a bit every second.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextRelapsePopup;
}

/// <summary>
/// One chemical, or a set of them, the body depends on.
/// </summary>
public sealed class ChemicalDependency
{
    /// <summary>
    /// The addiction picked up in the round. Null for the dependency the trait gives.
    /// </summary>
    [ViewVariables]
    public ProtoId<AddictionPrototype>? Addiction;

    /// <summary>
    /// Reagents any of which count towards a dose.
    /// </summary>
    [ViewVariables]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = new();

    [ViewVariables]
    public FixedPoint2 DoseAmount;

    [ViewVariables]
    public TimeSpan DoseDuration;

    /// <summary>
    /// How much of a treatment reagent cures the dependency.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 TreatmentAmount;

    /// <summary>
    /// How much of the chemicals was taken in towards the next dose.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 Consumed;

    /// <summary>
    /// When withdrawal starts unless a dose is taken.
    /// </summary>
    [ViewVariables]
    public TimeSpan WithdrawalAt;

    [ViewVariables]
    public bool Warned;

    [ViewVariables]
    public bool InWithdrawal;

    /// <summary>
    /// How long the body has been through the current bout of withdrawal, counted only while alive.
    /// </summary>
    [ViewVariables]
    public TimeSpan WithdrawalElapsed;

    /// <summary>
    /// How far the body is from getting over the addiction, from 0 to 1. Taking the chemical sets it back.
    /// </summary>
    [ViewVariables]
    public float Recovery;

    /// <summary>
    /// How much of the chemicals was taken in since the addiction was picked up, until it turns heavy.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 TakenWhileAddicted;

    /// <summary>
    /// The addiction got heavy from taking in too much: it is cured like the dependency from the trait.
    /// </summary>
    [ViewVariables]
    public bool Heavy;

    /// <summary>
    /// The dependency the trait gives: only a treatment reagent cures it, sitting out withdrawal does not.
    /// </summary>
    public bool Innate => Addiction == null;
}
