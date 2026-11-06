using System.Numerics;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Delivery;

[Serializable, NetSerializable]
public enum DeliveryUiMode : byte
{
    Maintenance,
    AccountUnavailable,
    Shop,
    Courier,
}

[Serializable, NetSerializable]
public readonly record struct DeliveryCatalogEntry(string Prototype, int Price);

[Serializable, NetSerializable]
public sealed class DeliveryCatalogSection
{
    public string Name = string.Empty;
    public List<DeliveryCatalogEntry> Entries = new();
}

[Serializable, NetSerializable]
public readonly record struct DeliveryOrderItem(string Prototype, int Count);

[Serializable, NetSerializable]
public sealed class DeliveryOrderInfo
{
    public int Id;
    public string Customer = string.Empty;
    public string? Courier;
    public bool TakenByViewer;
    public int Total;
    public int Payout;
    public TimeSpan ExpiresAt;
    public bool InTransit;
    public Vector2? Position;
    public List<DeliveryOrderItem> Items = new();
}

[Serializable, NetSerializable]
public sealed class DeliveryUiState : BoundUserInterfaceState
{
    public readonly DeliveryUiMode Mode;
    public readonly int? Balance;
    public readonly int CatalogHash;
    public readonly List<DeliveryCatalogSection>? Catalog;
    public readonly List<DeliveryOrderInfo> Orders;
    public readonly string CourierName;
    public readonly int Delivered;
    public readonly int Earned;

    public DeliveryUiState(DeliveryUiMode mode,
        int? balance,
        List<DeliveryOrderInfo> orders,
        int catalogHash = 0,
        List<DeliveryCatalogSection>? catalog = null,
        string courierName = "",
        int delivered = 0,
        int earned = 0)
    {
        Mode = mode;
        Balance = balance;
        Orders = orders;
        CatalogHash = catalogHash;
        Catalog = catalog;
        CourierName = courierName;
        Delivered = delivered;
        Earned = earned;
    }
}

[Serializable, NetSerializable]
public sealed class DeliveryPlaceOrderMessage : CartridgeMessageEvent
{
    public readonly Dictionary<string, int> Cart;

    public DeliveryPlaceOrderMessage(Dictionary<string, int> cart)
    {
        Cart = cart;
    }
}

[Serializable, NetSerializable]
public sealed class DeliveryTakeOrderMessage : CartridgeMessageEvent
{
    public readonly int OrderId;

    public DeliveryTakeOrderMessage(int orderId)
    {
        OrderId = orderId;
    }
}

[Serializable, NetSerializable]
public sealed class DeliveryRequestCatalogMessage : CartridgeMessageEvent;
