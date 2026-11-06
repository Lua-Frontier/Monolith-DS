using Content.Shared._LuaM.Delivery;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared.Popups;

namespace Content.Server._LuaM.Delivery;

public sealed partial class DeliverySystem
{
    private void OnUiReady(Entity<DeliveryCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(args.Loader, ent);
    }

    private void OnMessage(Entity<DeliveryCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);
        if (!TryGetHolder(loader, out var holder) || holder != args.Actor)
            return;

        switch (args)
        {
            case DeliveryRequestCatalogMessage:
                UpdateUi(loader, ent, includeCatalog: true);
                return;
            case DeliveryPlaceOrderMessage place:
                var result = TryPlaceOrder(holder, loader, place.Cart);
                if (result != DeliveryOrderResult.Success)
                    _popup.PopupEntity(Loc.GetString(GetErrorLoc(result)), holder, holder, PopupType.SmallCaution);
                break;
            case DeliveryTakeOrderMessage take:
                var takeResult = TryTakeOrder(holder, take.OrderId);
                _popup.PopupEntity(takeResult == DeliveryTakeResult.Success
                        ? Loc.GetString("delivery-order-taken", ("id", take.OrderId))
                        : Loc.GetString(GetTakeErrorLoc(takeResult), ("max", DeliveryRules.MaxCourierOrders)),
                    holder,
                    holder,
                    takeResult == DeliveryTakeResult.Success ? PopupType.Small : PopupType.SmallCaution);
                break;
            default:
                return;
        }

        UpdateUi(loader, ent);
    }

    private DeliveryUiMode GetMode(EntityUid loader, out EntityUid holder)
    {
        if (!TryGetHolder(loader, out holder))
            return DeliveryUiMode.AccountUnavailable;

        if (HasComp<DeliveryCourierComponent>(holder))
            return DeliveryUiMode.Courier;

        if (HasComp<IronmanComponent>(holder))
            return DeliveryUiMode.AccountUnavailable;

        return IsServiceAvailable() ? DeliveryUiMode.Shop : DeliveryUiMode.Maintenance;
    }

    private void UpdateUi(EntityUid loader, Entity<DeliveryCartridgeComponent> cartridge, bool includeCatalog = false)
    {
        var mode = GetMode(loader, out var holder);
        cartridge.Comp.LastMode = mode;

        var orders = new List<DeliveryOrderInfo>();
        DeliveryUiState state;

        switch (mode)
        {
            case DeliveryUiMode.Shop:
            {
                _bank.TryGetBalance(holder, out var balance);
                foreach (var order in _orders.Values)
                {
                    if (order.Customer == holder)
                        orders.Add(ToInfo(order, holder, false));
                }

                orders.Sort((a, b) => a.Id.CompareTo(b.Id));
                var catalog = GetCatalog(out var hash);
                state = new DeliveryUiState(mode, balance, orders, hash, includeCatalog ? catalog : null);
                break;
            }
            case DeliveryUiMode.Courier:
            {
                int? balance = null;
                if (!HasComp<IronmanComponent>(holder) && _bank.TryGetBalance(holder, out var bankBalance))
                    balance = bankBalance;

                foreach (var order in _orders.Values)
                {
                    orders.Add(ToInfo(order, holder, true));
                }

                orders.Sort((a, b) => a.Id.CompareTo(b.Id));
                var courier = Comp<DeliveryCourierComponent>(holder);
                state = new DeliveryUiState(mode,
                    balance,
                    orders,
                    courierName: Name(holder),
                    delivered: courier.Delivered,
                    earned: courier.Earned);
                break;
            }
            default:
                state = new DeliveryUiState(mode, null, orders);
                break;
        }

        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }

    private DeliveryOrderInfo ToInfo(DeliveryOrder order, EntityUid viewer, bool withPosition)
    {
        var info = new DeliveryOrderInfo
        {
            Id = order.Id,
            Customer = order.CustomerName,
            Courier = order.Courier is { } courier && IsCourierOnShift(courier) ? order.CourierName : null,
            TakenByViewer = order.Courier == viewer,
            Total = order.Total,
            Payout = DeliveryRules.GetCourierPayout(order.Total),
            ExpiresAt = order.ExpiresAt,
            InTransit = order.Parcel != null,
        };

        foreach (var line in order.Lines)
        {
            info.Items.Add(new DeliveryOrderItem(line.Prototype, line.Count));
        }

        if (withPosition && order.Parcel is { } parcel && !TerminatingOrDeleted(parcel))
            info.Position = _transform.GetWorldPosition(parcel);

        return info;
    }

    private void RefreshOpenApps(bool couriersOnly = false, bool checkModes = false)
    {
        var query = EntityQueryEnumerator<CartridgeLoaderComponent>();
        while (query.MoveNext(out var loader, out var loaderComp))
        {
            if (loaderComp.ActiveProgram is not { } program
                || !TryComp<DeliveryCartridgeComponent>(program, out var cartridge)
                || !_ui.IsUiOpen(loader, PdaUiKey.Key))
                continue;

            if (couriersOnly)
            {
                var mode = GetMode(loader, out _);
                var modeChanged = checkModes && mode != cartridge.LastMode;
                if (!modeChanged && (mode != DeliveryUiMode.Courier || _orders.Count == 0))
                    continue;
            }

            UpdateUi(loader, (program, cartridge));
        }
    }

    private static string GetTakeErrorLoc(DeliveryTakeResult result)
    {
        return result switch
        {
            DeliveryTakeResult.Unavailable => "delivery-error-unavailable",
            DeliveryTakeResult.TooManyOrders => "delivery-error-courier-too-many",
            _ => "delivery-error-take",
        };
    }

    private static string GetErrorLoc(DeliveryOrderResult result)
    {
        return result switch
        {
            DeliveryOrderResult.Unavailable => "delivery-error-unavailable",
            DeliveryOrderResult.NoAccount => "delivery-error-no-account",
            DeliveryOrderResult.NoIdCard => "delivery-error-no-id",
            DeliveryOrderResult.TooManyItems => "delivery-error-too-many-items",
            DeliveryOrderResult.TooCheap => "delivery-error-too-cheap",
            DeliveryOrderResult.TooManyOrders => "delivery-error-too-many-orders",
            DeliveryOrderResult.InsufficientFunds => "delivery-error-insufficient-funds",
            _ => "delivery-error-invalid-cart",
        };
    }
}
