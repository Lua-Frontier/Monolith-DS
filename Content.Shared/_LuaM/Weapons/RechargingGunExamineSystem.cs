using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Self-recharging guns, like the Anaconda or the Dragoon, show the damage and armor penetration of their bullet on examine.
/// </summary>
public sealed partial class RechargingGunExamineSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        // RechargeBasicEntityAmmoSystem already subscribes the recharge component to examine, so this goes through the ammo provider.
        SubscribeLocalEvent<BasicEntityAmmoProviderComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<BasicEntityAmmoProviderComponent> ent, ref ExaminedEvent args)
    {
        var provider = ent.Comp;
        if (!HasComp<RechargeBasicEntityAmmoComponent>(ent)
            || string.IsNullOrEmpty(provider.Proto.Id)
            || !_proto.TryIndex<EntityPrototype>(provider.Proto, out var bullet)
            || !bullet.TryGetComponent<ProjectileComponent>(out var projectile, _factory)
            || projectile.Damage.Empty)
            return;

        var damage = _damageable.ApplyUniversalAllModifiers(projectile.Damage * _damageable.UniversalProjectileDamageModifier);

        var parts = damage.DamageDict
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .Select(pair => Loc.GetString("recharging-gun-examine-damage-entry",
                ("type", _proto.TryIndex<DamageTypePrototype>(pair.Key, out var type) ? type.LocalizedName : pair.Key),
                ("amount", pair.Value.Float())))
            .ToList();

        if (parts.Count == 0)
            return;

        using (args.PushGroup(nameof(RechargingGunExamineSystem)))
        {
            args.PushMarkup(Loc.GetString("recharging-gun-examine-damage", ("damage", string.Join(", ", parts))));

            if (projectile.ArmorPenetration > 0)
            {
                var percent = (int) MathF.Round(projectile.ArmorPenetration * 100f);
                args.PushMarkup(Loc.GetString("recharging-gun-examine-penetration", ("percent", percent)));
            }
        }
    }
}
