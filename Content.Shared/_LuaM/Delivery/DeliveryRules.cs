using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Delivery;

public static class DeliveryRules
{
    public const int MinOrderTotal = 5_000;

    public const int MaxItems = 30;

    public const int MaxActiveOrders = 2;

    public const int MaxCourierOrders = 2;

    public const int PayoutThreshold = 50_000;

    public const int LowPayoutPercent = 30;

    public const int HighPayoutPercent = 10;

    public static readonly TimeSpan DeliveryTime = TimeSpan.FromMinutes(30);

    public static int GetCourierPayout(int total)
    {
        if (total <= 0)
            return 0;

        long low = Math.Min(total, PayoutThreshold);
        long high = Math.Max(total - PayoutThreshold, 0);
        return (int) (low * LowPayoutPercent / 100 + high * HighPayoutPercent / 100);
    }
}

[DataDefinition]
public sealed partial class DeliveryCatalogCategory
{
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public List<ProtoId<VendingMachineInventoryPrototype>> Inventories = new();

    // -- Предметы вне торгоматов, цена считается так же, если не задана в prices каталога
    [DataField]
    public List<EntProtoId> Items = new();
}

[Prototype("deliveryCatalog")]
public sealed partial class DeliveryCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<DeliveryCatalogCategory> Categories = new();

    // -- Остальные публичные торгоматы (без AccessReader) попадают в эту категорию
    [DataField]
    public LocId? OtherCategory = "delivery-category-other";

    [DataField]
    public List<ProtoId<VendingMachineInventoryPrototype>> ExcludeInventories = new();

    [DataField]
    public List<EntProtoId> Exclude = new();

    [DataField]
    public List<EntProtoId> ExcludeParents = new();

    [DataField]
    public List<string> ExcludeComponents = new();

    // -- Итоговая цена без наценки
    [DataField]
    public Dictionary<EntProtoId, int> Prices = new();

    [DataField]
    public float Markup = 2.25f;
}
