/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Globalization;
using System.Linq;
using Content.Server.Power.Components;
using Content.Shared._LuaM.Guidebook;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Guidebook;

public sealed partial class GuidebookCatalogSystem
{
    private void AddPersonalShieldStats(EntityPrototype item, List<GuidebookCatalogStat> stats)
    {
        if (!item.TryGetComponent<PersonalShieldComponent>(out var personal)) return;
        var shield = personal.Shield;
        void Add(string label, string value) => stats.Add(new() { Label = label, Value = value });
        static string F(double value) => value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));

        Add("Максимальный запас поля", F(shield.MaxCharge) + " ед.");
        Add("Восстановление поднятого поля", F(shield.RegenRate) + " ед./с");
        Add("Развёртывание", F(shield.SpinupTime) + " с");
        Add("Распад после разрушения", F(personal.Visuals.ShatterTime) + " с");
        Add("Задержка после распада", F(shield.BreakCooldown) + " с");
        Add("Повторное полное развёртывание", "≈" + F(personal.Visuals.ShatterTime + shield.BreakCooldown + shield.SpinupTime)
            + " с после пробоя, если хватает питания и костюм надет");

        foreach (var type in _prototype.EnumeratePrototypes<DamageTypePrototype>().OrderBy(t => t.LocalizedName))
        {
            var coefficient = shield.BlockDamageModifier.Coefficients.GetValueOrDefault(type.ID, 1f);
            var flat = shield.BlockDamageModifier.FlatReduction.GetValueOrDefault(type.ID);
            Add("Расход заряда: " + type.LocalizedName, "×" + F(coefficient)
                + (flat != 0 ? "; перед умножением вычитается " + F(flat) + " ед. урона" : ""));
        }

        var raw = _prototype.GetPrototypeData(item);
        var hasBattery = raw.TryGetValue("Battery", out var battery);
        var maxBattery = hasBattery ? Numeric(battery!, "maxCharge") : 0;
        var startingBattery = hasBattery ? Numeric(battery!, "startingCharge") : 0;
        Add("Потребление энергии", hasBattery ? F(shield.PowerDraw) + " Дж/с, пока поле работает или разворачивается"
            : "Внутренней батареи нет; расход энергии этой системой не применяется");
        if (hasBattery)
        {
            Add("Батарея", F(maxBattery) + " Дж; начальный заряд " + F(startingBattery) + " Дж");
            var recharge = item.TryGetComponent<BatterySelfRechargerComponent>(out var recharger) && recharger.AutoRecharge;
            var rate = recharge ? recharger!.AutoRechargeRate : 0;
            Add("Самозаряд батареи", recharge ? F(rate) + " Дж/с" : "Нет");
            if (recharge && recharger!.AutoRechargePause)
                Add("Пауза самозаряда после расхода", F(recharger.AutoRechargePauseTime) + " с");
            else if (shield.PowerDraw > 0)
            {
                var drain = shield.PowerDraw - rate;
                Add("Питание при непрерывной работе", drain > 0
                    ? "Расход батареи ≈" + F(drain) + " Дж/с; от полной ≈" + F(maxBattery / drain) + " с, без других потребителей"
                    : "Самозаряд покрывает постоянный расход поля");
            }
        }

        var blocksPlates = false;
        if (raw.TryGetValue("Storage", out var storage) && Node(storage, "blacklist") is Robust.Shared.Serialization.Markdown.Mapping.MappingDataNode blacklist)
        {
            var components = Values(Node(blacklist, "components")).ToList();
            blocksPlates = components.Contains("ArmorPlateItem")
                && (Scalar(blacklist, "requireAll") != "true" || components.Count == 1);
        }
        Add("Бронеплиты", blocksPlates ? "Вставка в хранилище запрещена"
            : item.Components.ContainsKey("ArmorPlateHolder") ? "Есть держатель; используются по обычным правилам"
            : "Держателя нет; плита в хранилище не добавит защиты");
    }
}
