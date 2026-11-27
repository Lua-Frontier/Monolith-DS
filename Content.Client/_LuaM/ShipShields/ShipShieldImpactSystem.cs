/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.ShipShields;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.ShipShields;

// -- Последние попадания по каждому щиту для шейдера: вспышка в точке и волна по полосе края
public sealed class ShipShieldImpactSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public const int MaxImpacts = 8;
    public const float Lifetime = 2.5f;

    private readonly Dictionary<EntityUid, List<Impact>> _impacts = new();
    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ShipShieldImpactEvent>(OnImpact);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _impacts.Clear();
    }

    private void OnImpact(ShipShieldImpactEvent ev)
    {
        if (!TryGetEntity(ev.Shield, out var shield))
            return;

        if (!_impacts.TryGetValue(shield.Value, out var list))
        {
            list = new List<Impact>(MaxImpacts);
            _impacts[shield.Value] = list;
        }

        if (list.Count >= MaxImpacts)
            list.RemoveAt(0);

        list.Add(new Impact(ev.LocalPosition, ev.Strength, _timing.RealTime));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_impacts.Count == 0)
            return;

        var now = _timing.RealTime;
        foreach (var (uid, list) in _impacts)
        {
            // runs every frame while a shield is hit: a plain loop instead of a lambda capturing now
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if ((now - list[i].Time).TotalSeconds >= Lifetime)
                    list.RemoveAt(i);
            }
            if (list.Count == 0 || TerminatingOrDeleted(uid))
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
            _impacts.Remove(uid);

        _expired.Clear();
    }

    public int Fill(EntityUid shield, Vector2[] positions, float[] ages, float[] strengths)
    {
        if (!_impacts.TryGetValue(shield, out var list))
            return 0;

        var now = _timing.RealTime;
        var count = Math.Min(list.Count, positions.Length);
        for (var i = 0; i < count; i++)
        {
            positions[i] = list[i].Position;
            ages[i] = (float) (now - list[i].Time).TotalSeconds;
            strengths[i] = list[i].Strength;
        }

        return count;
    }

    private readonly record struct Impact(Vector2 Position, float Strength, TimeSpan Time);
}
