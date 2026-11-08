using Content.Server._NF.Bank;
using System.Threading.Tasks;
using Content.Server.Cargo.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Preferences.Managers;
using Content.Server.Stack;
using Content.Shared._LuaM.Delivery;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared._Mono.VendingMachine;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.VendingMachines;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Delivery;

public enum DeliveryOrderResult : byte
{
    Success,
    Unavailable,
    NoAccount,
    NoIdCard,
    InvalidCart,
    TooManyItems,
    TooCheap,
    TooManyOrders,
    InsufficientFunds,
}

public enum DeliveryTakeResult : byte
{
    Success,
    Unavailable,
    Taken,
    TooManyOrders,
}

public sealed partial class DeliverySystem : EntitySystem
{
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PinpointerSystem _pinpointer = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string CashPrototype = "SpaceCash";
    private const double FallbackPrice = 20;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ModeCheckInterval = TimeSpan.FromSeconds(5);

    private readonly Dictionary<int, DeliveryOrder> _orders = new();
    private readonly Dictionary<string, int> _catalogPrices = new();
    private List<DeliveryCatalogSection>? _catalog;
    private int _catalogHash;
    private int _nextOrderId = 1;
    private TimeSpan _nextTick;
    private TimeSpan _nextModeCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeliveryParcelComponent, AfterInteractUsingEvent>(OnParcelInteractUsing);
        SubscribeLocalEvent<DeliveryParcelComponent, ExaminedEvent>(OnParcelExamined);
        SubscribeLocalEvent<DeliveryParcelComponent, ComponentShutdown>(OnParcelShutdown);

        SubscribeLocalEvent<DeliveryCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<DeliveryCartridgeComponent, CartridgeMessageEvent>(OnMessage);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;

        if (_orders.Count > 0)
        {
            List<DeliveryOrder>? expired = null;
            foreach (var order in _orders.Values)
            {
                if (now >= order.ExpiresAt)
                    (expired ??= new()).Add(order);
            }

            if (expired != null)
            {
                foreach (var order in expired)
                    ExpireOrder(order);

                RefreshOpenApps();
            }
        }

        var checkModes = now >= _nextModeCheck;
        if (checkModes)
            _nextModeCheck = now + ModeCheckInterval;

        RefreshOpenApps(couriersOnly: true, checkModes: checkModes);
    }

    #region Catalog

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DeliveryCatalogPrototype>()
            || args.WasModified<VendingMachineInventoryPrototype>()
            || args.WasModified<EntityPrototype>())
            _catalog = null;
    }

    private List<DeliveryCatalogSection> GetCatalog(out int hash)
    {
        if (_catalog == null)
            BuildCatalog();

        hash = _catalogHash;
        return _catalog!;
    }

    private void BuildCatalog()
    {
        _catalogPrices.Clear();
        _catalog = new List<DeliveryCatalogSection>();

        var publicInventories = new Dictionary<string, float>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract
                || !proto.TryGetComponent<VendingMachineComponent>(out var vending, _factory)
                || proto.Components.ContainsKey(_factory.GetComponentName<AccessReaderComponent>()))
                continue;

            var modifier = proto.TryGetComponent<MarketModifierComponent>(out var market, _factory) ? market.Mod : 1f;
            var pack = vending.PackPrototypeId.Id;
            publicInventories[pack] = publicInventories.TryGetValue(pack, out var current)
                ? Math.Min(current, modifier)
                : modifier;
        }

        foreach (var catalog in _proto.EnumeratePrototypes<DeliveryCatalogPrototype>())
        {
            var used = new HashSet<string>();
            foreach (var inventory in catalog.ExcludeInventories)
                used.Add(inventory.Id);

            foreach (var category in catalog.Categories)
            {
                var section = new DeliveryCatalogSection { Name = category.Name };
                foreach (var inventory in category.Inventories)
                {
                    if (!used.Add(inventory.Id))
                        continue;

                    AddInventory(catalog, section, inventory.Id, publicInventories.GetValueOrDefault(inventory.Id, 1f));
                }

                foreach (var item in category.Items)
                    AddEntry(catalog, section, item, 1f);

                AddSection(section);
            }

            if (catalog.OtherCategory is not { } other)
                continue;

            var otherSection = new DeliveryCatalogSection { Name = other };
            var leftovers = new List<string>();
            foreach (var inventory in publicInventories.Keys)
            {
                if (!used.Contains(inventory))
                    leftovers.Add(inventory);
            }

            leftovers.Sort(string.CompareOrdinal);
            foreach (var inventory in leftovers)
                AddInventory(catalog, otherSection, inventory, publicInventories[inventory]);

            AddSection(otherSection);
        }

        _catalogHash = ComputeCatalogHash(_catalog);
    }

    private void AddSection(DeliveryCatalogSection section)
    {
        if (section.Entries.Count == 0)
            return;

        foreach (var existing in _catalog!)
        {
            if (existing.Name != section.Name)
                continue;

            existing.Entries.AddRange(section.Entries);
            return;
        }

        _catalog!.Add(section);
    }

    private void AddInventory(DeliveryCatalogPrototype catalog, DeliveryCatalogSection section, string inventoryId, float modifier)
    {
        if (!_proto.TryIndex<VendingMachineInventoryPrototype>(inventoryId, out var inventory))
            return;

        foreach (var protoId in inventory.StartingInventory.Keys)
            AddEntry(catalog, section, protoId, modifier);
    }

    private void AddEntry(DeliveryCatalogPrototype catalog, DeliveryCatalogSection section, EntProtoId protoId, float modifier)
    {
        if (catalog.Exclude.Contains(protoId)
            || _catalogPrices.ContainsKey(protoId.Id)
            || !_proto.TryIndex(protoId, out var entityProto)
            || entityProto.Abstract
            || IsExcluded(catalog, entityProto))
            return;

        var price = catalog.Prices.TryGetValue(protoId, out var fixedPrice)
            ? fixedPrice
            : (int) Math.Ceiling(GetVendingPrice(entityProto, modifier) * catalog.Markup);

        if (price <= 0)
            return;

        _catalogPrices[protoId.Id] = price;
        section.Entries.Add(new DeliveryCatalogEntry(protoId.Id, price));
    }

    private bool IsExcluded(DeliveryCatalogPrototype catalog, EntityPrototype proto)
    {
        foreach (var component in catalog.ExcludeComponents)
        {
            if (proto.Components.ContainsKey(component))
                return true;
        }

        if (catalog.ExcludeParents.Count == 0)
            return false;

        // -- EnumerateParents пропускает абстрактных родителей вместе с их предками, поэтому цепочку проходим по YAML
        var queue = new Queue<string>(proto.Parents ?? Array.Empty<string>());
        var seen = new HashSet<string>();
        while (queue.TryDequeue(out var id))
        {
            if (!seen.Add(id))
                continue;

            if (catalog.ExcludeParents.Contains(id))
                return true;

            if (!_proto.TryGetMapping(typeof(EntityPrototype), id, out var mapping)
                || !mapping.TryGet("parent", out var parents))
                continue;

            if (parents is ValueDataNode single)
                queue.Enqueue(single.Value);
            else if (parents is SequenceDataNode list)
            {
                foreach (var node in list)
                {
                    if (node is ValueDataNode value)
                        queue.Enqueue(value.Value);
                }
            }
        }

        return false;
    }

    private double GetVendingPrice(EntityPrototype proto, float modifier)
    {
        var vendPrice = _pricing.GetEstimatedVendPrice(proto);
        if (vendPrice > 0)
            return vendPrice;

        var price = _pricing.GetEstimatedPrice(proto);
        if (price == 0)
            price = FallbackPrice;

        return price * modifier;
    }

    private static int ComputeCatalogHash(List<DeliveryCatalogSection> catalog)
    {
        var hash = new HashCode();
        foreach (var section in catalog)
        {
            hash.Add(section.Name, StringComparer.Ordinal);
            foreach (var entry in section.Entries)
            {
                hash.Add(entry.Prototype, StringComparer.Ordinal);
                hash.Add(entry.Price);
            }
        }

        var result = hash.ToHashCode();
        return result == 0 ? 1 : result;
    }

    #endregion

    #region Orders

    public bool HasCouriersOnShift()
    {
        var query = EntityQueryEnumerator<DeliveryCourierComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (!_mobState.IsDead(uid))
                return true;
        }

        return false;
    }

    private bool IsCourierOnShift(EntityUid uid)
    {
        return HasComp<DeliveryCourierComponent>(uid) && HasComp<ActorComponent>(uid) && !_mobState.IsDead(uid);
    }

    private bool TryGetTelepad(out Entity<DeliveryTelepadComponent> telepad)
    {
        var query = EntityQueryEnumerator<DeliveryTelepadComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            telepad = (uid, comp);
            return true;
        }

        telepad = default;
        return false;
    }

    public bool IsServiceAvailable()
    {
        return HasCouriersOnShift() && TryGetTelepad(out _);
    }

    private int CountActiveOrders(EntityUid customer)
    {
        var count = 0;
        foreach (var order in _orders.Values)
        {
            if (order.Customer == customer)
                count++;
        }

        return count;
    }

    public DeliveryOrderResult TryPlaceOrder(EntityUid customer, EntityUid loader, Dictionary<string, int> cart)
    {
        if (!HasComp<BankAccountComponent>(customer)
            || HasComp<IronmanComponent>(customer)
            || HasComp<DeliveryCourierComponent>(customer))
            return DeliveryOrderResult.NoAccount;

        if (!IsServiceAvailable())
            return DeliveryOrderResult.Unavailable;

        if (!_idCard.TryGetIdCard(loader, out var idCard) || string.IsNullOrWhiteSpace(idCard.Comp.FullName))
            return DeliveryOrderResult.NoIdCard;

        if (CountActiveOrders(customer) >= DeliveryRules.MaxActiveOrders)
            return DeliveryOrderResult.TooManyOrders;

        GetCatalog(out _);

        var lines = new List<DeliveryOrderLine>();
        var count = 0;
        long total = 0;
        foreach (var (proto, amount) in cart)
        {
            if (amount <= 0)
                continue;

            if (amount > DeliveryRules.MaxItems)
                return DeliveryOrderResult.TooManyItems;

            if (!_catalogPrices.TryGetValue(proto, out var price))
                return DeliveryOrderResult.InvalidCart;

            count += amount;
            if (count > DeliveryRules.MaxItems)
                return DeliveryOrderResult.TooManyItems;

            total += (long) price * amount;
            lines.Add(new DeliveryOrderLine(proto, amount, price));
        }

        if (lines.Count == 0 || total > int.MaxValue)
            return DeliveryOrderResult.InvalidCart;

        if (total < DeliveryRules.MinOrderTotal)
            return DeliveryOrderResult.TooCheap;

        if (!_bank.TryBankWithdraw(customer, (int) total))
            return DeliveryOrderResult.InsufficientFunds;

        _player.TryGetSessionByEntity(customer, out var session);

        var order = new DeliveryOrder
        {
            Id = _nextOrderId++,
            Customer = customer,
            CustomerUser = session?.UserId,
            CustomerName = idCard.Comp.FullName!,
            CustomerProfileName = Name(customer),
            Lines = lines,
            Total = (int) total,
            ExpiresAt = _timing.CurTime + DeliveryRules.DeliveryTime,
        };

        _orders[order.Id] = order;

        _popup.PopupEntity(Loc.GetString("delivery-order-placed", ("id", order.Id)), customer, customer);
        NotifyCouriers(Loc.GetString("delivery-notify-new-order",
            ("id", order.Id),
            ("customer", order.CustomerName),
            ("payout", DeliveryRules.GetCourierPayout(order.Total))));

        RefreshOpenApps();
        return DeliveryOrderResult.Success;
    }

    private void SpawnParcel(DeliveryOrder order, Entity<DeliveryTelepadComponent> telepad)
    {
        var telepadXform = Transform(telepad);
        order.OriginGrid = telepadXform.GridUid;

        var parcel = SpawnAtPosition(telepad.Comp.ParcelPrototype, telepadXform.Coordinates);
        var parcelComp = EnsureComp<DeliveryParcelComponent>(parcel);
        parcelComp.OrderId = order.Id;
        order.Parcel = parcel;

        if (TryComp<PinpointerComponent>(parcel, out var pinpointer))
        {
            _pinpointer.SetTarget(parcel, order.Customer, pinpointer);
            if (!pinpointer.IsActive)
                _pinpointer.TogglePinpointer(parcel, pinpointer);
        }

        _audio.PlayPvs(telepad.Comp.ArriveSound, telepad);
    }

    private int CountCourierOrders(EntityUid courier)
    {
        var count = 0;
        foreach (var order in _orders.Values)
        {
            if (order.Courier == courier)
                count++;
        }

        return count;
    }

    public DeliveryTakeResult TryTakeOrder(EntityUid courier, int orderId)
    {
        if (!HasComp<DeliveryCourierComponent>(courier)
            || !_orders.TryGetValue(orderId, out var order)
            || order.Courier is { } current && IsCourierOnShift(current))
            return DeliveryTakeResult.Taken;

        if (CountCourierOrders(courier) >= DeliveryRules.MaxCourierOrders)
            return DeliveryTakeResult.TooManyOrders;

        Entity<DeliveryTelepadComponent> telepad = default;
        if (order.Parcel == null && !TryGetTelepad(out telepad))
            return DeliveryTakeResult.Unavailable;

        _player.TryGetSessionByEntity(courier, out var session);
        order.Courier = courier;
        order.CourierName = Name(courier);
        order.CourierUser = session?.UserId;

        if (order.Parcel == null)
            SpawnParcel(order, telepad);

        if (Exists(order.Customer))
        {
            _popup.PopupEntity(Loc.GetString("delivery-order-in-transit", ("id", order.Id), ("courier", order.CourierName)),
                order.Customer,
                order.Customer);
        }

        RefreshOpenApps();
        return DeliveryTakeResult.Success;
    }

    private void OnParcelInteractUsing(Entity<DeliveryParcelComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach || !_idCard.TryGetIdCard(args.Used, out var idCard))
            return;

        args.Handled = true;

        if (!_orders.TryGetValue(ent.Comp.OrderId, out var order))
            return;

        if (args.User != order.Customer || idCard.Comp.FullName != order.CustomerName)
        {
            _popup.PopupEntity(Loc.GetString("delivery-parcel-wrong-customer"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        Deliver(ent, order, args.User);
    }

    private void Deliver(Entity<DeliveryParcelComponent> parcel, DeliveryOrder order, EntityUid customer)
    {
        _orders.Remove(order.Id);

        var coords = _transform.GetMoverCoordinates(parcel);
        foreach (var line in order.Lines)
        {
            for (var i = 0; i < line.Count; i++)
            {
                var item = SpawnAtPosition(line.Prototype, coords);
                if (order.OriginGrid is not { } grid)
                    continue;

                var purchase = EnsureComp<VendingMachinePurchaseComponent>(item);
                purchase.PurchaseGrid = grid;
                purchase.OriginalPurchasePrice = line.Price;
                Dirty(item, purchase);
            }
        }

        _audio.PlayPvs(parcel.Comp.OpenSound, coords);
        _popup.PopupEntity(Loc.GetString("delivery-parcel-opened"), customer, customer);
        QueueDel(parcel);

        if (order.Courier is { } courier && order.CourierName is { } courierName)
            PayCourier(order, courier, courierName);

        RefreshOpenApps();
    }

    private void PayCourier(DeliveryOrder order, EntityUid courier, string courierName)
    {
        var payout = DeliveryRules.GetCourierPayout(order.Total);
        var paidInCash = Exists(courier) && HasComp<IronmanComponent>(courier);

        if (paidInCash)
        {
            foreach (var cash in _stack.SpawnMultiple(CashPrototype, payout, courier))
                _hands.PickupOrDrop(courier, cash);
        }
        else
        {
            Deposit(courier, order.CourierUser, courierName, payout, true, $"delivery order {order.Id} payout");
        }

        if (!TryComp<DeliveryCourierComponent>(courier, out var courierComp))
            return;

        courierComp.Delivered++;
        courierComp.Earned += payout;
        _popup.PopupEntity(Loc.GetString(paidInCash ? "delivery-courier-paid-cash" : "delivery-courier-paid", ("amount", payout)),
            courier,
            courier);
    }

    private void ExpireOrder(DeliveryOrder order)
    {
        _orders.Remove(order.Id);

        if (order.Parcel is { } parcel
            && !TerminatingOrDeleted(parcel)
            && TryComp<DeliveryParcelComponent>(parcel, out var parcelComp))
        {
            var coords = _transform.GetMoverCoordinates(parcel);
            _audio.PlayPvs(parcelComp.BreakSound, coords);
            SpawnAtPosition(parcelComp.BrokenSpawn, coords);
            QueueDel(parcel);
        }

        Refund(order);

        var message = Loc.GetString("delivery-order-expired", ("id", order.Id));
        if (Exists(order.Customer))
            _popup.PopupEntity(message, order.Customer, order.Customer, PopupType.MediumCaution);

        if (order.Courier is { } courier && Exists(courier))
            _popup.PopupEntity(message, courier, courier, PopupType.MediumCaution);
    }

    private void OnParcelShutdown(Entity<DeliveryParcelComponent> ent, ref ComponentShutdown args)
    {
        if (!_orders.Remove(ent.Comp.OrderId, out var order))
            return;

        Refund(order);
        RefreshOpenApps();
    }

    private void Refund(DeliveryOrder order)
    {
        Deposit(order.Customer, order.CustomerUser, order.CustomerProfileName, order.Total, false, $"delivery order {order.Id} refund");
    }

    private void Deposit(EntityUid mob, NetUserId? user, string profileName, int amount, bool tax, string reason)
    {
        if (amount <= 0)
            return;

        if (Exists(mob) && _player.TryGetSessionByEntity(mob, out _))
        {
            if (!_bank.TryBankDeposit(mob, amount, tax))
                Log.Error($"Failed to deposit {amount} to {ToPrettyString(mob)} for {reason}");

            return;
        }

        if (user is not { } userId)
        {
            Log.Error($"Failed to deposit {amount} to {profileName} for {reason}: no user");
            return;
        }

        _ = DepositOfflineAsync(userId, profileName, amount, reason);
    }

    private async Task DepositOfflineAsync(NetUserId user, string profileName, int amount, string reason)
    {
        try
        {
            var prefs = _prefs.TryGetCachedPreferences(user, out var cached)
                ? cached
                : await _db.GetPlayerPreferencesAsync(user, default);

            if (prefs != null)
            {
                foreach (var profile in prefs.Characters.Values)
                {
                    if (profile is HumanoidCharacterProfile humanoid
                        && humanoid.Name == profileName
                        && await _bank.TryBankDepositOffline(user, prefs, humanoid, amount))
                        return;
                }
            }

            Log.Error($"Failed to deposit {amount} to {profileName} ({user}) for {reason}");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to deposit {amount} to {profileName} ({user}) for {reason}: {e}");
        }
    }

    private void OnParcelExamined(Entity<DeliveryParcelComponent> ent, ref ExaminedEvent args)
    {
        if (!_orders.TryGetValue(ent.Comp.OrderId, out var order))
            return;

        var left = order.ExpiresAt - _timing.CurTime;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;

        args.PushMarkup(Loc.GetString("delivery-parcel-examine-order", ("id", order.Id)));
        args.PushMarkup(Loc.GetString("delivery-parcel-examine-customer", ("name", order.CustomerName)));
        args.PushMarkup(Loc.GetString("delivery-parcel-examine-courier",
            ("name", order.CourierName ?? Loc.GetString("delivery-courier-none"))));
        args.PushMarkup(Loc.GetString("delivery-parcel-examine-time", ("time", FormatTime(left))));
    }

    private void NotifyCouriers(string message)
    {
        var header = Loc.GetString("delivery-program-name");
        var query = EntityQueryEnumerator<CartridgeLoaderComponent>();
        while (query.MoveNext(out var loader, out var loaderComp))
        {
            if (!TryGetHolder(loader, out var holder) || !HasComp<DeliveryCourierComponent>(holder))
                continue;

            _cartridgeLoader.SendNotification(loader, header, message, loaderComp);
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New != GameRunLevel.PostRound || _orders.Count == 0)
            return;

        foreach (var order in new List<DeliveryOrder>(_orders.Values))
            ExpireOrder(order);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _orders.Clear();
        _nextOrderId = 1;
    }

    public static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalMinutes:D2}:{time.Seconds:D2}";
    }

    #endregion

    private bool TryGetHolder(EntityUid loader, out EntityUid holder)
    {
        var current = loader;
        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            current = container.Owner;
            if (!HasComp<BankAccountComponent>(current) && !HasComp<DeliveryCourierComponent>(current))
                continue;

            holder = current;
            return true;
        }

        holder = default;
        return false;
    }

    private sealed class DeliveryOrder
    {
        public int Id;
        public EntityUid Customer;
        public NetUserId? CustomerUser;
        public string CustomerName = string.Empty;
        public string CustomerProfileName = string.Empty;
        public EntityUid? Courier;
        public string? CourierName;
        public NetUserId? CourierUser;
        public List<DeliveryOrderLine> Lines = new();
        public int Total;
        public EntityUid? Parcel;
        public EntityUid? OriginGrid;
        public TimeSpan ExpiresAt;
    }

    private readonly record struct DeliveryOrderLine(string Prototype, int Count, int Price);
}
