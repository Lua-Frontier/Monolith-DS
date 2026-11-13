/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Guidebook;

public sealed partial class GuidebookCatalogSystem
{
    private void AddMedicalStats(EntityPrototype item, List<GuidebookCatalogStat> stats)
    {
        void Add(string label, string value) => stats.Add(new() { Label = label, Value = value });
        if (item.Description.Length > 0) Add("Назначение", item.Description);
        var data = _prototype.GetPrototypeData(item);
        if (data.TryGetValue("CyberwareHumanity", out var humanity)) Add("Человечность", Numeric(humanity, "cost").ToString("0.##"));
        if (data.TryGetValue("Organ", out var organ) && Scalar(organ, "slotId") is { } slot) Add("Слот органа", SlotName(slot));
        if (data.TryGetValue("AutoSurgeon", out var surgeon))
        {
            Add("Установка", "Пристегните пациента; без пациента устройство оперирует активировавшего.");
            if (Scalar(surgeon, "targetBodyPart") is { } part) Add("Область операции", SlotName(part));
            if (Scalar(surgeon, "targetOrgan") is { } target && target != "null") Add("Замена слота", SlotName(target));
            if (Scalar(surgeon, "upgradeTier") is { } tier) Add("Уровень улучшения", tier);
            if (Scalar(surgeon, "humanityCost") is { } cost) Add("Человечность улучшения", cost);
        }
    }

    private static string SlotName(string slot) => slot.ToLowerInvariant() switch
    {
        "torso" => "туловище", "head" => "голова", "arm" => "рука", "hand" => "кисть",
        "leg" => "нога", "foot" => "стопа", "heart" => "сердце", "lungs" => "лёгкие",
        "liver" => "печень", "kidneys" => "почки", "stomach" => "желудок", "brain" => "мозг",
        "eyes" => "глаза", "cns" => "ЦНС", "subdermal" => "подкожная броня",
        "humanitychip" or "chip" => "нейростабилизатор", "armimplant" => "имплант руки",
        _ => slot,
    };
}
