using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Adds the armor penetration and pellet count of the bullet a cartridge fires to the cartridge's examine text.
/// </summary>
public sealed partial class AmmoArmorPenetrationExamineSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;

    /// <summary>
    /// Adds the examine lines for the projectile or ballistic hitscan prototype a cartridge fires.
    /// Ammo without such damage data gets nothing.
    /// </summary>
    public void AddExamine(ExaminedEvent args, EntProtoId projectileProto)
    {
        if (!_proto.TryIndex(projectileProto, out var proto))
            return;

        proto.TryComp<ProjectileSpreadComponent>(out var spread, _factory);

        if (!TryGetArmorPenetration(proto, spread, out var penetration))
            return;

        var percent = (int) MathF.Round(penetration * 100f);
        var color = percent switch
        {
            > 0 => "lightgreen",
            < 0 => "red",
            _ => "gray",
        };

        args.PushMarkup(Loc.GetString("ammo-armor-penetration-examine", ("color", color), ("percent", percent)));

        if (spread is { Count: > 1 })
            args.PushMarkup(Loc.GetString("ammo-pellet-count-examine", ("count", spread.Count)));
    }

    private bool TryGetArmorPenetration(EntityPrototype proto, ProjectileSpreadComponent? spread, out float penetration)
    {
        // Rounds ignoring resistances go through armor entirely, whatever their armor penetration says.
        if (proto.TryComp<ProjectileComponent>(out var projectile, _factory))
        {
            penetration = projectile.IgnoreResistances ? 1f : projectile.ArmorPenetration;
            return true;
        }

        // Ballistic hitscan rounds (e.g. 14.5x114mm) deal their damage through HitscanBasicDamage.
        if (proto.TryComp<HitscanBasicDamageComponent>(out var hitscan, _factory))
        {
            penetration = hitscan.IgnoreResistances ? 1f : hitscan.ArmorPenetration;
            return true;
        }

        // Shot spreads may only define the pellet they shoot.
        if (spread != null && _proto.TryIndex(spread.Proto, out var pelletProto))
            return TryGetArmorPenetration(pelletProto, null, out penetration);

        penetration = 0f;
        return false;
    }
}
