using Content.Server.Chat.Systems;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared._LuaM.Traits;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Traits;

public sealed partial class FurAllergySystem : EntitySystem
{
    [Dependency] private FurAllergyBlurSystem _blur = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FoodSystem _food = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<Entity<HumanoidAppearanceComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, InteractionSuccessEvent>(OnHug);
    }

    private void OnHug(Entity<HumanoidAppearanceComponent> target, ref InteractionSuccessEvent args)
    {
        // Works both ways: an allergic hugging a furry, or a furry hugging an allergic.
        if (TryComp<FurAllergyComponent>(target, out var targetAllergy)
            && TryComp<HumanoidAppearanceComponent>(args.User, out var user)
            && targetAllergy.Species.Contains(user.Species))
            AddHugExposure((target, targetAllergy));

        if (TryComp<FurAllergyComponent>(args.User, out var userAllergy)
            && userAllergy.Species.Contains(target.Comp.Species))
            AddHugExposure((args.User, userAllergy));
    }

    private void AddHugExposure(Entity<FurAllergyComponent> ent)
    {
        if (_mobState.IsDead(ent) || _food.IsMouthBlocked(ent))
            return;

        ent.Comp.Exposure = Math.Min(ent.Comp.Exposure + ent.Comp.HugExposure, ent.Comp.MaxExposure);
        // Let the next tick show the new stage right away.
        ent.Comp.NextUpdate = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FurAllergyComponent>();
        while (query.MoveNext(out var uid, out var allergy))
        {
            if (now < allergy.NextUpdate)
                continue;

            allergy.NextUpdate = now + UpdateInterval;

            if (_mobState.IsDead(uid))
                continue;

            var seconds = (float) UpdateInterval.TotalSeconds;
            if (IsExposed((uid, allergy)))
                allergy.Exposure = Math.Min(allergy.Exposure + seconds, allergy.MaxExposure);
            else
                allergy.Exposure = Math.Max(allergy.Exposure - allergy.RecoveryRate * seconds, 0f);

            var stage = allergy.Exposure >= allergy.SevereThreshold ? FurAllergyStage.Severe
                : allergy.Exposure >= allergy.MildThreshold ? FurAllergyStage.Mild
                : FurAllergyStage.None;

            if (stage != allergy.Stage)
                OnStageChanged((uid, allergy), stage, now);

            if (stage == FurAllergyStage.None)
                continue;

            if (now >= allergy.NextCough)
            {
                allergy.NextCough = now + RandomCoughDelay(allergy);
                _chat.TryEmoteWithChat(uid, stage == FurAllergyStage.Severe ? "Gasp" : "Cough", ignoreActionBlocker: true);
            }

            if (stage != FurAllergyStage.Severe)
                continue;

            _damageable.TryChangeDamage(uid, allergy.ChokeDamage * seconds, ignoreResistances: true, interruptsDoAfters: false);

            if (now >= allergy.NextShock)
            {
                allergy.NextShock = now + allergy.ShockInterval;
                _popup.PopupEntity(Loc.GetString("fur-allergy-shock"), uid, uid, PopupType.MediumCaution);
                _stun.TryParalyze(uid, allergy.ShockDuration, true);
            }
        }
    }

    private bool IsExposed(Entity<FurAllergyComponent> ent)
    {
        // A mask or a sealed helmet keeps the fur out.
        if (_food.IsMouthBlocked(ent))
            return false;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _nearby);
        foreach (var other in _nearby)
        {
            if (other.Owner != ent.Owner && ent.Comp.Species.Contains(other.Comp.Species))
                return true;
        }

        return false;
    }

    private void OnStageChanged(Entity<FurAllergyComponent> ent, FurAllergyStage stage, TimeSpan now)
    {
        var worse = stage > ent.Comp.Stage;
        _blur.SetStage(ent, stage);

        if (!worse)
        {
            if (stage == FurAllergyStage.None)
                _popup.PopupEntity(Loc.GetString("fur-allergy-gone"), ent, ent);
            return;
        }

        if (stage == FurAllergyStage.Mild)
        {
            ent.Comp.NextCough = now + RandomCoughDelay(ent.Comp);
            _popup.PopupEntity(Loc.GetString("fur-allergy-mild"), ent, ent, PopupType.SmallCaution);
        }
        else
        {
            ent.Comp.NextShock = now + ent.Comp.ShockInterval;
            _popup.PopupEntity(Loc.GetString("fur-allergy-severe"), ent, ent, PopupType.LargeCaution);
        }
    }

    private TimeSpan RandomCoughDelay(FurAllergyComponent allergy)
    {
        return allergy.CoughMin + (allergy.CoughMax - allergy.CoughMin) * _random.NextDouble();
    }
}
