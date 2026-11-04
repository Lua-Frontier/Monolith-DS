/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Globalization;
using System.Linq;
using Content.Shared._LuaM.Guidebook;

namespace Content.Client._LuaM.Guidebook;

public static class GuidebookCatalogPresentation
{
    public static List<GuidebookCatalogStat> Summary(GuidebookEquipmentEntry item)
    {
        var result = new List<GuidebookCatalogStat>();
        void Pick(string key, string label)
        {
            if (item.Stats.FirstOrDefault(s => s.Label == key) is { } stat && Useful(stat))
                result.Add(new GuidebookCatalogStat { Label = label, Value = stat.Value });
        }
        if (item.Kind == "server")
        {
            Pick("Вычислительная мощность", "Мощность СУО");
            Pick("Лимит консолей", "Консоли");
            Pick("Нагрузка приёмника питания", "Питание");
            return result;
        }
        Pick("Пополнение", "Боезапас");
        var rateStat = item.Stats.FirstOrDefault(s => s.Label == "Теоретический средний темп очередями")
            ?? item.Stats.FirstOrDefault(s => s.Label == "Темп вне очереди");
        if (rateStat != null && Number(rateStat.Value) is { } rate && rate > 0)
            result.Add(new() { Label = "Темп", Value = F(rate * 60) + " выстр./мин" + (rate < 1 ? " (один за " + F(1 / rate) + " с)" : "") });
        var ranges = item.Stats.Where(s => s.Ammunition && (s.Label.EndsWith("дальность луча") || s.Label.Contains("теоретический путь")))
            .Select(s => Number(s.Value)).Where(v => v > 0).Select(v => v!.Value).Distinct().Order().ToList();
        var beam = item.Stats.Any(s => s.Label.EndsWith("дальность луча"));
        if (ranges.Count > 0)
            result.Add(new() { Label = beam ? "Дальность луча" : "Примерная дальность", Value =
                (ranges.Count == 1 ? F(ranges[0]) : F(ranges[0]) + "–" + F(ranges[^1])) + " м" });
        var speed = item.Stats.FirstOrDefault(s => s.Ammunition && s.Label.EndsWith("скорость, заданная орудием"));
        if (beam) result.Add(new() { Label = "Полёт", Value = "Луч достигает цели сразу" });
        else if (speed != null && Number(speed.Value) is { } velocity && velocity > 0)
            result.Add(new() { Label = "Полёт на 100 м", Value = "около " + F(100 / velocity) + " с (" + F(velocity) + " м/с)" });
        Pick("Начальный магазин", "Магазин");
        if (!result.Any(s => s.Label == "Магазин")) Pick("Начальный боеприпас", "Боеприпас");
        Pick("Восстановление", "Зарядка");
        return result;
    }

    private static double? Number(string value) => double.TryParse(value.Split(' ')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    private static string F(double value) => value.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU"));

    public static bool Useful(GuidebookCatalogStat stat) => stat.Value is not ("0" or "0 Дж" or "0 Дж/с" or "0 с" or "нет" or "Нет")
        && stat.Label is not ("Начальный заряд" or "Режим при установке" or "КПД зарядки батареи" or "Совместимые боеприпасы / магазины");

    public static string DamageValue(string value)
    {
        // The effective value is what a player compares; the raw multiplier remains in details.
        var arrow = value.IndexOf(" → ", StringComparison.Ordinal);
        const string suffix = " после множителя орудия";
        return arrow >= 0 && value.EndsWith(suffix, StringComparison.Ordinal)
            ? value[(arrow + 3)..^suffix.Length] : value;
    }
}
