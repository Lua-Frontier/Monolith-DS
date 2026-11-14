using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Dependency on one chemical, picked at random when the body spawns. Every <see cref="DoseAmount"/> of it the body
/// takes in holds withdrawal off for <see cref="DoseDuration"/>. In withdrawal the body shakes, falls, drops what it
/// holds and makes noises.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ChemicalDependencyComponent : Component
{
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
    /// The chemical the body depends on. Picked at random on spawn when not set.
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
    /// How long before withdrawal the body starts to crave the chemical.
    /// </summary>
    [DataField]
    public TimeSpan WarningTime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How often withdrawal rolls its effects.
    /// </summary>
    [DataField]
    public TimeSpan EffectInterval = TimeSpan.FromSeconds(4);

    [DataField]
    public float KnockdownChance = 0.3f;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(3);

    [DataField]
    public float DropChance = 0.3f;

    [DataField]
    public float EmoteChance = 0.35f;

    [DataField]
    public List<ProtoId<EmotePrototype>> Emotes = new() { "Scream", "Cough", "Crying", "Gasp", "Sneeze" };

    [DataField]
    public float PopupChance = 0.2f;

    [DataField]
    public List<LocId> Popups = new()
    {
        "chemical-dependency-withdrawal-1",
        "chemical-dependency-withdrawal-2",
        "chemical-dependency-withdrawal-3",
    };

    /// <summary>
    /// How much of the chemical was taken in towards the next dose.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 Consumed;

    /// <summary>
    /// When withdrawal starts unless a dose is taken.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan WithdrawalAt;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextEffect;

    [ViewVariables]
    public bool Warned;

    [ViewVariables]
    public bool InWithdrawal;
}
