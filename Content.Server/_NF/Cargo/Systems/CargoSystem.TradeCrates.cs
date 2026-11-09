using Content.Server._NF.Trade;
using Content.Shared._NF.Trade;
using Content.Shared.Examine;

namespace Content.Server.Cargo.Systems; // Needs to collide with base namespace

public sealed partial class CargoSystem
{
    private readonly List<EntityUid> _destinations = new();
    // LuaM start
    // -- Одна очередь по сроку вместо Timer.Spawn на каждый ящик: за тик проверяется только ближайший срок
    private readonly PriorityQueue<EntityUid, TimeSpan> _expressQueue = new();
    // LuaM end

    private void InitializeTradeCrates()
    {
        SubscribeLocalEvent<TradeCrateComponent, PriceCalculationEvent>(OnTradeCrateGetPriceEvent);
        SubscribeLocalEvent<TradeCrateComponent, ComponentInit>(OnTradeCrateInit);
        SubscribeLocalEvent<TradeCrateComponent, ExaminedEvent>(OnTradeCrateExamined);

        SubscribeLocalEvent<TradeCrateDestinationComponent, ComponentInit>(OnDestinationInit);
        SubscribeLocalEvent<TradeCrateDestinationComponent, ComponentRemove>(OnDestinationRemove);
    }

    private void OnTradeCrateGetPriceEvent(Entity<TradeCrateComponent> ent, ref PriceCalculationEvent ev)
    {
        var owningStation = _station.GetOwningStation(ent);
        var atDestination = ent.Comp.DestinationStation != EntityUid.Invalid
                           && owningStation == ent.Comp.DestinationStation
                           || HasComp<TradeCrateWildcardDestinationComponent>(owningStation);
        ev.Price = atDestination ? ent.Comp.ValueAtDestination : ent.Comp.ValueElsewhere;
        if (ent.Comp.ExpressDeliveryTime != null)
        {
            if (_timing.CurTime <= ent.Comp.ExpressDeliveryTime && atDestination)
                ev.Price += ent.Comp.ExpressOnTimeBonus;
            else if (_timing.CurTime > ent.Comp.ExpressDeliveryTime)
                ev.Price -= ent.Comp.ExpressLatePenalty;
        }
        // Mono Begin
        var valueMultiplier = 1f;
        if (TryComp<TradeCrateWildcardDestinationComponent>(owningStation, out var wildcardComp))
        {
            valueMultiplier = wildcardComp.ValueMultiplier;
        }
        ev.Price *= valueMultiplier;
        // Mono End
        ev.Price = double.Max(0.0, ev.Price); // Ensure non-negative values.
    }

    private void OnTradeCrateInit(Entity<TradeCrateComponent> ent, ref ComponentInit ev)
    {
        // If there are no available destinations, tough luck.
        if (_destinations.Count > 0)
        {
            var randomIndex = _random.Next(_destinations.Count);
            // Better have more than one destination.
            if (_station.GetOwningStation(ent) == _destinations[randomIndex])
            {
                randomIndex = (randomIndex + 1 + _random.Next(_destinations.Count - 1)) % _destinations.Count;
            }
            var destination = _destinations[randomIndex];
            ent.Comp.DestinationStation = destination;
            if (TryComp<TradeCrateDestinationComponent>(destination, out var destComp))
                _appearance.SetData(ent, TradeCrateVisuals.DestinationIcon, destComp.DestinationProto.Id);
        }

        if (ent.Comp.ExpressDeliveryDuration > TimeSpan.Zero)
        {
            ent.Comp.ExpressDeliveryTime = _timing.CurTime + ent.Comp.ExpressDeliveryDuration;
            _appearance.SetData(ent, TradeCrateVisuals.IsPriority, true);
            _expressQueue.Enqueue(ent, ent.Comp.ExpressDeliveryTime.Value); // LuaM
        }
    }

    // LuaM start
    private void UpdateTradeCrates()
    {
        var now = _timing.CurTime;
        while (_expressQueue.TryPeek(out var uid, out var due) && due <= now)
        {
            _expressQueue.Dequeue();
            if (!TryComp<TradeCrateComponent>(uid, out var crate) || crate.ExpressDeliveryTime is not { } deadline)
                continue;

            // -- На паузе срок ещё не сдвинут: перепроверим позже, а после снятия паузы возьмём новый срок
            if (MetaData(uid).EntityPaused)
            {
                _expressQueue.Enqueue(uid, now + TimeSpan.FromSeconds(10));
                continue;
            }

            if (deadline > now)
            {
                _expressQueue.Enqueue(uid, deadline);
                continue;
            }

            DisableTradeCratePriority(uid);
        }
    }
    // LuaM end

    // TODO: move to shared, share delivery time?
    private void OnTradeCrateExamined(Entity<TradeCrateComponent> ent, ref ExaminedEvent ev)
    {
        if (!TryComp(ent.Comp.DestinationStation, out MetaDataComponent? metadata))
            return;

        ev.PushMarkup(Loc.GetString("trade-crate-destination-station", ("destination", metadata.EntityName)));

        if (ent.Comp.ExpressDeliveryTime == null)
            return;

        ev.PushMarkup(ent.Comp.ExpressDeliveryTime >= _timing.CurTime ?
            Loc.GetString("trade-crate-priority-active") :
            Loc.GetString("trade-crate-priority-inactive"));

        // LuaM start
        // -- Показываем остаток или просрочку вместо момента от начала смены
        var left = ent.Comp.ExpressDeliveryTime.Value - _timing.CurTime;
        ev.PushMarkup(left >= TimeSpan.Zero
            ? Loc.GetString("luam-trade-crate-time-left", ("time", FormatTradeCrateTime(left)))
            : Loc.GetString("luam-trade-crate-past-due", ("time", FormatTradeCrateTime(-left))));
        // LuaM end
    }

    // LuaM start
    private static string FormatTradeCrateTime(TimeSpan time)
    {
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
    }
    // LuaM end

    private void DisableTradeCratePriority(EntityUid uid)
    {
        _appearance.SetData(uid, TradeCrateVisuals.IsPriorityInactive, true);
    }

    private void OnDestinationInit(Entity<TradeCrateDestinationComponent> ent, ref ComponentInit ev)
    {
        if (!_destinations.Contains(ent))
            _destinations.Add(ent);
    }

    private void OnDestinationRemove(Entity<TradeCrateDestinationComponent> ent, ref ComponentRemove ev)
    {
        _destinations.Remove(ent);
    }

    private void CleanupTradeCrateDestinations()
    {
        _destinations.Clear();
        _expressQueue.Clear(); // LuaM
    }
}
