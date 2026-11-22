using System.Linq;
using Content.Server._LuaM.Body;
using Content.Server.Chat.Systems;
using Content.Shared._LuaM.Addiction;
using Content.Shared._LuaM.Traits;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Traits;

public sealed partial class ChemicalDependencySystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Every reagent some reaction makes, cached until prototypes reload.
    /// </summary>
    private HashSet<ProtoId<ReagentPrototype>>? _craftable;

    private readonly List<ChemicalDependency> _cured = new();

    private readonly HashSet<ChemicalWithdrawalEffects> _withdrawalEffects = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalDependencyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChemicalDependencyComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<ChemicalDependencyComponent, ReagentMetabolizedEvent>(OnMetabolized);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReactionPrototype>() || args.WasModified<ReagentPrototype>())
            _craftable = null;
    }

    private void OnMapInit(Entity<ChemicalDependencyComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;
        if (!comp.PickRandom)
            return;

        comp.Reagent ??= PickReagent(comp);
        if (comp.Reagent is not { } reagent)
            return;

        comp.Dependencies.Add(new ChemicalDependency
        {
            Reagents = { reagent },
            DoseAmount = comp.DoseAmount,
            DoseDuration = comp.DoseDuration,
            TreatmentAmount = comp.TreatmentAmount,
            WithdrawalAt = _timing.CurTime + comp.DoseDuration,
        });
    }

    private void OnUnpaused(Entity<ChemicalDependencyComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextEffect += args.PausedTime;
        ent.Comp.NextRelapsePopup += args.PausedTime;
        foreach (var dependency in ent.Comp.Dependencies)
        {
            dependency.WithdrawalAt += args.PausedTime;
        }
    }

    private ProtoId<ReagentPrototype>? PickReagent(ChemicalDependencyComponent comp)
    {
        _craftable ??= _proto.EnumeratePrototypes<ReactionPrototype>()
            .SelectMany(reaction => reaction.Products.Keys)
            .ToHashSet();

        var candidates = new List<ProtoId<ReagentPrototype>>();
        foreach (var id in _craftable)
        {
            if (comp.Blacklist.Contains(id)
                || !_proto.TryIndex(id, out var reagent)
                || reagent.Metabolisms == null // the body could never take it in
                || !comp.Groups.Contains(reagent.Group))
                continue;

            candidates.Add(id);
        }

        if (candidates.Count == 0)
            return null;

        return _random.Pick(candidates);
    }

    private void OnMetabolized(Entity<ChemicalDependencyComponent> ent, ref ReagentMetabolizedEvent args)
    {
        var now = _timing.CurTime;
        var relieved = false;
        var relapsed = false;
        foreach (var dependency in ent.Comp.Dependencies)
        {
            if (!dependency.Reagents.Contains(args.Reagent))
                continue;

            // every unit of the chemical takes back its share of the progress towards getting over the dependency
            if (dependency.Recovery > 0f && dependency.DoseAmount > 0)
            {
                var relapse = ent.Comp.RelapsePerDose * args.Amount.Float() / dependency.DoseAmount.Float();
                dependency.Recovery = MathF.Max(0f, dependency.Recovery - relapse);
                relapsed = true;
            }

            CountTowardsHeavy(ent, dependency, args.Amount);

            dependency.Consumed += args.Amount;
            if (dependency.Consumed < dependency.DoseAmount)
                continue;

            // a full dose holds withdrawal off
            dependency.Consumed = 0;
            dependency.WithdrawalAt = now + dependency.DoseDuration;
            dependency.Warned = false;

            if (!dependency.InWithdrawal)
                continue;

            dependency.InWithdrawal = false;
            relieved = true;
        }

        if (relieved && !InWithdrawal(ent.Comp))
            _popup.PopupEntity(Loc.GetString("chemical-dependency-relief"), ent, ent);

        if (relapsed && _timing.CurTime >= ent.Comp.NextRelapsePopup)
        {
            ent.Comp.NextRelapsePopup = _timing.CurTime + TimeSpan.FromSeconds(30);
            _popup.PopupEntity(Loc.GetString("chemical-dependency-relapse"), ent, ent, PopupType.SmallCaution);
        }
    }

    /// <summary>
    /// Turns the addiction heavy once the body took in too much of it while addicted.
    /// </summary>
    private void CountTowardsHeavy(EntityUid uid, ChemicalDependency dependency, FixedPoint2 amount)
    {
        if (dependency.Heavy
            || dependency.Addiction is not { } addictionId
            || !_proto.TryIndex(addictionId, out var addiction)
            || addiction.HeavyThreshold is not { } threshold)
            return;

        dependency.TakenWhileAddicted += amount;
        if (dependency.TakenWhileAddicted <= threshold)
            return;

        dependency.Heavy = true;

        // the treatment already taken keeps its units, they are just a smaller share of the longer course
        if (addiction.HeavyTreatmentAmount > 0)
        {
            dependency.Recovery *= dependency.TreatmentAmount.Float() / addiction.HeavyTreatmentAmount.Float();
            dependency.TreatmentAmount = addiction.HeavyTreatmentAmount;
        }

        _popup.PopupEntity(Loc.GetString("addiction-heavy", ("addiction", Loc.GetString(addiction.Name))),
            uid, uid, PopupType.LargeCaution);
    }

    /// <summary>
    /// Whether the body already depends on the addiction, or on one of its reagents from spawn.
    /// </summary>
    public bool DependsOn(EntityUid uid, ProtoId<AddictionPrototype> addiction, IReadOnlySet<ProtoId<ReagentPrototype>> reagents)
    {
        if (!TryComp<ChemicalDependencyComponent>(uid, out var comp))
            return false;

        foreach (var dependency in comp.Dependencies)
        {
            if (dependency.Addiction == addiction)
                return true;

            if (dependency.Innate && dependency.Reagents.Overlaps(reagents))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Makes the body addicted. The first dose it already took holds withdrawal off for a while.
    /// </summary>
    public void AddAddiction(EntityUid uid, AddictionPrototype addiction, IEnumerable<ProtoId<ReagentPrototype>> reagents)
    {
        if (!TryComp<ChemicalDependencyComponent>(uid, out var comp))
        {
            comp = new ChemicalDependencyComponent { PickRandom = false };
            AddComp(uid, comp);
        }

        comp.Dependencies.Add(new ChemicalDependency
        {
            Addiction = addiction.ID,
            Reagents = reagents.ToHashSet(),
            DoseAmount = addiction.DoseAmount,
            DoseDuration = addiction.DoseDuration,
            TreatmentAmount = addiction.TreatmentAmount,
            WithdrawalAt = _timing.CurTime + addiction.DoseDuration,
        });

        _popup.PopupEntity(Loc.GetString("addiction-acquired", ("addiction", Loc.GetString(addiction.Name))),
            uid, uid, PopupType.MediumCaution);
    }

    /// <summary>
    /// A treatment reagent at work: holds every withdrawal off for <paramref name="relief"/>
    /// and brings every dependency closer to a cure, the one from the trait too.
    /// </summary>
    public void Treat(EntityUid uid, FixedPoint2 amount, TimeSpan relief)
    {
        if (!TryComp<ChemicalDependencyComponent>(uid, out var comp))
            return;

        var now = _timing.CurTime;
        var relieved = false;
        _cured.Clear();
        foreach (var dependency in comp.Dependencies)
        {
            if (dependency.TreatmentAmount > 0)
            {
                dependency.Recovery += amount.Float() / dependency.TreatmentAmount.Float();
                if (dependency.Recovery >= 1f)
                {
                    _cured.Add(dependency);
                    continue;
                }
            }

            if (dependency.WithdrawalAt >= now + relief)
                continue;

            dependency.WithdrawalAt = now + relief;
            dependency.Warned = true; // no craving popups every tick of the treatment

            if (!dependency.InWithdrawal)
                continue;

            dependency.InWithdrawal = false;
            relieved = true;
        }

        Cure((uid, comp));

        if (relieved && !InWithdrawal(comp))
            _popup.PopupEntity(Loc.GetString("chemical-dependency-relief"), uid, uid);
    }

    /// <summary>
    /// Removes every dependency in <see cref="_cured"/>, and the component once none is left.
    /// </summary>
    private void Cure(Entity<ChemicalDependencyComponent> ent)
    {
        foreach (var dependency in _cured)
        {
            ent.Comp.Dependencies.Remove(dependency);

            if (GetName(dependency) is { } name)
                _popup.PopupEntity(Loc.GetString("addiction-cured", ("addiction", name)), ent, ent, PopupType.Medium);
        }

        _cured.Clear();

        if (ent.Comp.Dependencies.Count == 0)
            RemCompDeferred<ChemicalDependencyComponent>(ent);
    }

    /// <summary>
    /// What the dependency is to: the addiction's name, or the chemical's for the one from the trait.
    /// </summary>
    private string? GetName(ChemicalDependency dependency)
    {
        if (dependency.Addiction is { } addictionId)
            return _proto.TryIndex(addictionId, out var addiction) ? Loc.GetString(addiction.Name) : null;

        foreach (var reagent in dependency.Reagents)
        {
            if (_proto.TryIndex(reagent, out var proto))
                return proto.LocalizedName;
        }

        return null;
    }

    private static bool InWithdrawal(ChemicalDependencyComponent comp)
    {
        foreach (var dependency in comp.Dependencies)
        {
            if (dependency.InWithdrawal)
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ChemicalDependencyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Dependencies.Count == 0)
                continue;

            var alive = _mobState.IsAlive(uid);
            var startedWithdrawal = false;
            var endured = false;
            _cured.Clear();

            foreach (var dependency in comp.Dependencies)
            {
                if (now < dependency.WithdrawalAt)
                {
                    if (!dependency.Warned && now >= dependency.WithdrawalAt - comp.WarningTime)
                    {
                        dependency.Warned = true;
                        _popup.PopupEntity(Loc.GetString("chemical-dependency-craving"), uid, uid);
                    }

                    continue;
                }

                if (!dependency.InWithdrawal)
                {
                    dependency.InWithdrawal = true;
                    dependency.WithdrawalElapsed = TimeSpan.Zero;
                    startedWithdrawal = true;
                }

                if (!alive)
                    continue;

                dependency.WithdrawalElapsed += TimeSpan.FromSeconds(frameTime);

                // sitting out withdrawal without a dose slowly gets the body over an addiction, unless it got heavy
                if (!dependency.Heavy
                    && dependency.Addiction is { } addictionId
                    && _proto.TryIndex(addictionId, out var addiction))
                {
                    dependency.Recovery += frameTime / (float) addiction.RecoveryTime.TotalSeconds;
                    if (dependency.Recovery >= 1f)
                    {
                        _cured.Add(dependency);
                        continue;
                    }
                }

                // a bout sat out lets go until the next one
                if (dependency.WithdrawalElapsed < comp.WithdrawalDuration)
                    continue;

                dependency.InWithdrawal = false;
                dependency.WithdrawalAt = now + dependency.DoseDuration;
                dependency.Warned = false;
                endured = true;
            }

            if (_cured.Count > 0)
                Cure((uid, comp));

            if (!InWithdrawal(comp))
            {
                if (endured)
                    _popup.PopupEntity(Loc.GetString("chemical-dependency-endured"), uid, uid);

                continue;
            }

            if (startedWithdrawal)
            {
                comp.NextEffect = now;
                _popup.PopupEntity(Loc.GetString("chemical-dependency-withdrawal-start"), uid, uid, PopupType.MediumCaution);
            }

            if (now < comp.NextEffect)
                continue;

            comp.NextEffect = now + comp.EffectInterval;

            if (alive)
                Withdrawal((uid, comp));
        }
    }

    private void Withdrawal(Entity<ChemicalDependencyComponent> ent)
    {
        // every dependency in withdrawal brings its own effects; the usual ones cover any milder ones
        _withdrawalEffects.Clear();
        foreach (var dependency in ent.Comp.Dependencies)
        {
            if (!dependency.InWithdrawal)
                continue;

            if (dependency.Addiction is { } addictionId
                && _proto.TryIndex(addictionId, out var addiction)
                && addiction.Withdrawal is { } own)
            {
                _withdrawalEffects.Add(own);
            }
            else
            {
                _withdrawalEffects.Add(ent.Comp.Withdrawal);
            }
        }

        if (_withdrawalEffects.Contains(ent.Comp.Withdrawal))
        {
            ApplyWithdrawal(ent, ent.Comp.Withdrawal);
            return;
        }

        foreach (var effects in _withdrawalEffects)
        {
            ApplyWithdrawal(ent, effects);
        }
    }

    private void ApplyWithdrawal(Entity<ChemicalDependencyComponent> ent, ChemicalWithdrawalEffects effects)
    {
        if (effects.Jitter)
            _jitter.DoJitter(ent, ent.Comp.EffectInterval + TimeSpan.FromSeconds(1), true, amplitude: 6f, frequency: 4f);

        if (effects.KnockdownChance > 0f && _random.Prob(effects.KnockdownChance))
            _stun.TryKnockdown(ent, effects.KnockdownTime, true);

        if (effects.DropChance > 0f && _random.Prob(effects.DropChance))
        {
            foreach (var held in _hands.EnumerateHeld(ent).ToList())
            {
                _hands.TryDrop(ent, held, checkActionBlocker: false);
            }
        }

        if (effects.Emotes.Count > 0 && _random.Prob(effects.EmoteChance))
            _chat.TryEmoteWithChat(ent, _random.Pick(effects.Emotes), ignoreActionBlocker: true);

        if (effects.Popups.Count > 0 && _random.Prob(effects.PopupChance))
            _popup.PopupEntity(Loc.GetString(_random.Pick(effects.Popups)), ent, ent, PopupType.SmallCaution);
    }
}
