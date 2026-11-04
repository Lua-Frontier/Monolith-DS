/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Damage;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Server._LuaM.Guidebook;

public sealed partial class GuidebookCatalogSystem
{
    private void AddHandheldStats(EntityPrototype item, List<GuidebookCatalogStat> stats)
    {
        var components = EquipmentComponents(item);
        var gun = Component(components, "Gun");
        void Add(string label, string value) => stats.Add(new() { Label = label, Value = value });
        Add("Темп", Format(Numeric(gun, "fireRate") * 60) + " выстр./мин");
        var actual = (GunComponent) item.Components["Gun"].Component;
        var min = actual.MinAngle.Degrees;
        var max = actual.MaxAngle.Degrees;
        // Gun angles are the width of the firing cone; report its radius at ten metres.
        Add("Разброс", Format(min) + "–" + Format(max) + "°; на 10 м отклонение до ±" + Format(Math.Tan(max * Math.PI / 360) * 10) + " м");
        Add("Отдача камеры", actual.CameraRecoilScalar == 0 ? "отсутствует" : Format(actual.CameraRecoilScalar / .5) + "× обычной");
        var ammo = DefaultAmmo(components);
        var visited = new HashSet<string>();
        var multiplier = Numeric(gun, "damageModifier", 1);
        var pellets = 1.0;
        while (ammo != null && visited.Add(ammo) && _prototype.TryIndex<EntityPrototype>(ammo, out var prototype))
        {
            var data = EquipmentComponents(prototype);
            foreach (var component in new[] { "Projectile", "HitscanBasicDamage" })
            {
                if (Node(Component(data, component), "damage") is not { } damage) continue;
                var spec = _serialization.Read<DamageSpecifier>(damage, notNullableOverride: true);
                var types = spec.DamageDict.Where(d => d.Value != 0).Select(d => DamageName(d.Key) + " " + (d.Value * (float) multiplier));
                Add("Штатный выстрел", prototype.Name + " — " + string.Join(" + ", types) + (pellets > 1 ? " × " + Format(pellets) + " дробин" : ""));
                return;
            }
            var spread = Component(data, "ProjectileSpread");
            if (Scalar(spread, "proto") is { } pellet)
            {
                pellets *= Numeric(spread, "count", 1);
                ammo = pellet;
            }
            else ammo = Scalar(Component(data, "CartridgeAmmo"), "proto") ?? DefaultAmmo(data);
        }
    }

    private static string? DefaultAmmo(Dictionary<string, MappingDataNode> data)
    {
        foreach (var provider in new[] { "ProjectileBatteryAmmoProvider", "HitscanBatteryAmmoProvider", "BallisticAmmoProvider", "RevolverAmmoProvider", "BasicEntityAmmoProvider" })
            if (Scalar(Component(data, provider), "proto") is { } proto && proto != "null") return proto;
        if (Node(Component(data, "ItemSlots"), "slots") is MappingDataNode slots)
            foreach (var (key, value) in slots.OrderByDescending(p => p.Key.Contains("magazine", StringComparison.OrdinalIgnoreCase)))
                if ((key.Contains("magazine", StringComparison.OrdinalIgnoreCase) || key.Contains("chamber", StringComparison.OrdinalIgnoreCase))
                    && value is MappingDataNode slot && Scalar(slot, "startingItem") is { } loaded && loaded != "null") return loaded;
        return null;
    }
}
