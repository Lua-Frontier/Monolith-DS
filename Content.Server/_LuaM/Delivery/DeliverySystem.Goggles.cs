using Content.Shared._LuaM.Delivery;

namespace Content.Server._LuaM.Delivery;

public sealed partial class DeliverySystem
{
    private readonly Dictionary<EntityUid, List<NetEntity>> _targets = new();

    private void UpdateCourierTargets()
    {
        foreach (var list in _targets.Values)
        {
            list.Clear();
        }

        foreach (var order in _orders.Values)
        {
            if (order.Courier is not { } courier || order.Parcel == null || !Exists(order.Customer))
                continue;

            if (!_targets.TryGetValue(courier, out var list))
                _targets[courier] = list = new List<NetEntity>();

            list.Add(GetNetEntity(order.Customer));
        }

        var query = EntityQueryEnumerator<DeliveryCourierComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _targets.TryGetValue(uid, out var list);
            if (list == null || list.Count == 0)
            {
                RemCompDeferred<DeliveryTargetsComponent>(uid);
                continue;
            }

            var comp = EnsureComp<DeliveryTargetsComponent>(uid);
            if (comp.Targets.Count == list.Count && comp.Targets.TrueForAll(list.Contains))
                continue;

            comp.Targets.Clear();
            comp.Targets.AddRange(list);
            Dirty(uid, comp);
        }

        foreach (var uid in new List<EntityUid>(_targets.Keys))
        {
            if (_targets[uid].Count == 0)
                _targets.Remove(uid);
        }
    }
}
