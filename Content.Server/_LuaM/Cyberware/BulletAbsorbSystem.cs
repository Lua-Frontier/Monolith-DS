using Content.Shared._LuaM.Cyberware;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Cyberware;

/// <summary>
/// Lets armor fully absorb one bullet every so often.
/// </summary>
public sealed partial class BulletAbsorbSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BulletAbsorbComponent, DamageModifyEvent>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BulletAbsorbComponent>();
        while (query.MoveNext(out var uid, out var absorb))
        {
            if (!absorb.WaitingForReady || _timing.CurTime < absorb.NextAbsorb)
                continue;

            absorb.WaitingForReady = false;
            _popup.PopupEntity(Loc.GetString(absorb.ReadyPopup), uid, uid);
        }
    }

    // DamageModifyEvent is raised by value, like in DamageProtectionBuffSystem.
    private void OnDamageModify(EntityUid uid, BulletAbsorbComponent comp, DamageModifyEvent args)
    {
        var ent = new Entity<BulletAbsorbComponent>(uid, comp);
        if (_timing.CurTime < ent.Comp.NextAbsorb
            || args.Tool is not { } tool
            || !HasComp<ProjectileComponent>(tool)
            || !args.Damage.AnyPositive())
            return;

        // Only bullets, energy bolts go through.
        if (TryComp<ReflectiveComponent>(tool, out var reflective)
            && (reflective.Reflective & ReflectType.NonEnergy) == 0)
            return;

        args.Damage = new DamageSpecifier();
        ent.Comp.NextAbsorb = _timing.CurTime + ent.Comp.Cooldown;
        ent.Comp.WaitingForReady = true;

        _audio.PlayPvs(ent.Comp.AbsorbSound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.AbsorbPopup), ent, PopupType.Medium);
    }
}
