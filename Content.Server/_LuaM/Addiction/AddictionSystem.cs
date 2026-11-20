using Content.Server._LuaM.Body;
using Content.Server._LuaM.Traits;
using Content.Server.Body.Components;
using Content.Shared._LuaM.Addiction;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Addiction;

/// <summary>
/// Counts how much of addictive chemicals a humanoid takes in and makes it addicted once it takes in
/// too much of one within the addiction's window.
/// </summary>
public sealed partial class AddictionSystem : EntitySystem
{
    [Dependency] private ChemicalDependencySystem _dependency = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Intake within this much time adds up into one bucket.
    /// </summary>
    private static readonly TimeSpan BucketLength = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The share of the threshold at which the body is told it is getting hooked.
    /// </summary>
    private const float WarningFraction = 0.66f;

    /// <summary>
    /// Addictions each reagent feeds, cached until prototypes reload.
    /// </summary>
    private Dictionary<ProtoId<ReagentPrototype>, List<AddictionPrototype>>? _byReagent;

    /// <summary>
    /// Every reagent that feeds each addiction, parents resolved.
    /// </summary>
    private readonly Dictionary<ProtoId<AddictionPrototype>, HashSet<ProtoId<ReagentPrototype>>> _reagents = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, ReagentMetabolizedEvent>(OnMetabolized);
        SubscribeLocalEvent<AddictionTrackerComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AddictionPrototype>() || args.WasModified<ReagentPrototype>())
            _byReagent = null;
    }

    private void OnUnpaused(Entity<AddictionTrackerComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var intake in ent.Comp.Intake.Values)
        {
            for (var i = 0; i < intake.Buckets.Count; i++)
            {
                var bucket = intake.Buckets[i];
                intake.Buckets[i] = (bucket.Start + args.PausedTime, bucket.Amount);
            }
        }
    }

    private Dictionary<ProtoId<ReagentPrototype>, List<AddictionPrototype>> GetLookup()
    {
        if (_byReagent != null)
            return _byReagent;

        _byReagent = new();
        _reagents.Clear();

        foreach (var addiction in _proto.EnumeratePrototypes<AddictionPrototype>())
        {
            var reagents = new HashSet<ProtoId<ReagentPrototype>>(addiction.Reagents);
            if (addiction.ReagentParents.Count > 0)
            {
                foreach (var reagent in _proto.EnumeratePrototypes<ReagentPrototype>())
                {
                    foreach (var (parent, _) in _proto.EnumerateAllParents<ReagentPrototype>(reagent.ID))
                    {
                        if (!addiction.ReagentParents.Contains(parent))
                            continue;

                        reagents.Add(reagent.ID);
                        break;
                    }
                }
            }

            _reagents[addiction.ID] = reagents;
            foreach (var reagent in reagents)
            {
                _byReagent.GetOrNew(reagent).Add(addiction);
            }
        }

        return _byReagent;
    }

    private FixedPoint2 GetBloodQuantity(EntityUid body, HashSet<ProtoId<ReagentPrototype>> reagents)
    {
        if (!TryComp<BloodstreamComponent>(body, out var bloodstream)
            || !_solutions.TryGetSolution(body, bloodstream.ChemicalSolutionName, out _, out var blood))
            return FixedPoint2.Zero;

        var total = FixedPoint2.Zero;
        foreach (var reagent in reagents)
        {
            total += blood.GetTotalPrototypeQuantity(reagent);
        }

        return total;
    }

    private void OnMetabolized(Entity<BodyComponent> ent, ref ReagentMetabolizedEvent args)
    {
        if (!GetLookup().TryGetValue(args.Reagent, out var addictions)
            || !HasComp<HumanoidAppearanceComponent>(ent))
            return;

        var tracker = EnsureComp<AddictionTrackerComponent>(ent);
        var now = _timing.CurTime;

        foreach (var addiction in addictions)
        {
            var reagents = _reagents[addiction.ID];
            if (_dependency.DependsOn(ent, addiction.ID, reagents))
            {
                tracker.Intake.Remove(addiction.ID);
                continue;
            }

            if (addiction.OverdoseThreshold is { } overdose)
            {
                // the reagent is already taken out of the blood, so it counts towards what was there
                if (GetBloodQuantity(ent, reagents) + args.Amount >= overdose)
                    _dependency.AddAddiction(ent, addiction, reagents);

                continue;
            }

            var intake = tracker.Intake.GetOrNew(addiction.ID);

            // forget what was taken in before the window
            var cutoff = now - addiction.Window;
            while (intake.Buckets.Count > 0 && intake.Buckets[0].Start < cutoff)
            {
                intake.Total -= intake.Buckets[0].Amount;
                intake.Buckets.RemoveAt(0);
            }

            var last = intake.Buckets.Count - 1;
            if (last >= 0 && intake.Buckets[last].Start + BucketLength > now)
                intake.Buckets[last] = (intake.Buckets[last].Start, intake.Buckets[last].Amount + args.Amount);
            else
                intake.Buckets.Add((now, args.Amount));

            intake.Total += args.Amount;

            if (intake.Total >= addiction.Threshold)
            {
                tracker.Intake.Remove(addiction.ID);
                _dependency.AddAddiction(ent, addiction, reagents);
                continue;
            }

            if (intake.Total < addiction.Threshold * WarningFraction)
            {
                intake.Warned = false;
            }
            else if (!intake.Warned)
            {
                intake.Warned = true;
                _popup.PopupEntity(Loc.GetString("addiction-warning", ("addiction", Loc.GetString(addiction.Name))),
                    ent, ent, PopupType.SmallCaution);
            }
        }
    }
}
