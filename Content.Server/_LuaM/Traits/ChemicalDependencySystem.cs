using System.Linq;
using Content.Server._LuaM.Body;
using Content.Server.Chat.Systems;
using Content.Shared._LuaM.Traits;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalDependencyComponent, MapInitEvent>(OnMapInit);
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
        ent.Comp.Reagent ??= PickReagent(ent.Comp);
        ent.Comp.WithdrawalAt = _timing.CurTime + ent.Comp.DoseDuration;
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
        var comp = ent.Comp;
        if (args.Reagent != comp.Reagent)
            return;

        comp.Consumed += args.Amount;
        if (comp.Consumed < comp.DoseAmount)
            return;

        comp.Consumed = 0;
        comp.WithdrawalAt = _timing.CurTime + comp.DoseDuration;
        comp.Warned = false;

        if (!comp.InWithdrawal)
            return;

        comp.InWithdrawal = false;
        _popup.PopupEntity(Loc.GetString("chemical-dependency-relief"), ent, ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ChemicalDependencyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Reagent == null)
                continue;

            if (now < comp.WithdrawalAt)
            {
                if (!comp.Warned && now >= comp.WithdrawalAt - comp.WarningTime)
                {
                    comp.Warned = true;
                    _popup.PopupEntity(Loc.GetString("chemical-dependency-craving"), uid, uid);
                }

                continue;
            }

            if (!comp.InWithdrawal)
            {
                comp.InWithdrawal = true;
                comp.NextEffect = now;
                _popup.PopupEntity(Loc.GetString("chemical-dependency-withdrawal-start"), uid, uid, PopupType.MediumCaution);
            }

            if (now < comp.NextEffect)
                continue;

            comp.NextEffect = now + comp.EffectInterval;

            if (_mobState.IsAlive(uid))
                Withdrawal((uid, comp));
        }
    }

    private void Withdrawal(Entity<ChemicalDependencyComponent> ent)
    {
        var comp = ent.Comp;

        _jitter.DoJitter(ent, comp.EffectInterval + TimeSpan.FromSeconds(1), true, amplitude: 6f, frequency: 4f);

        if (_random.Prob(comp.KnockdownChance))
            _stun.TryKnockdown(ent, comp.KnockdownTime, true);

        if (_random.Prob(comp.DropChance))
        {
            foreach (var held in _hands.EnumerateHeld(ent).ToList())
            {
                _hands.TryDrop(ent, held, checkActionBlocker: false);
            }
        }

        if (comp.Emotes.Count > 0 && _random.Prob(comp.EmoteChance))
            _chat.TryEmoteWithChat(ent, _random.Pick(comp.Emotes), ignoreActionBlocker: true);

        if (comp.Popups.Count > 0 && _random.Prob(comp.PopupChance))
            _popup.PopupEntity(Loc.GetString(_random.Pick(comp.Popups)), ent, ent, PopupType.SmallCaution);
    }
}
