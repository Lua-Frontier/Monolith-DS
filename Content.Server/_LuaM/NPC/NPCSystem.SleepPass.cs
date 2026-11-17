using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.NPC.Systems;

// -- Проверка «спать/проснуться» раскидана по тикам и сравнивает с заранее собранными позициями игроков вместо NPC x игроки за один тик.
public sealed partial class NPCSystem
{
    private const int SleepPassTicks = 45;

    private readonly List<EntityUid> _sleepQueue = new();
    private readonly Dictionary<MapId, List<Vector2>> _sleepPlayers = new();
    private int _sleepIndex;
    private int _sleepBatch;

    private void StartSleepPass()
    {
        foreach (var list in _sleepPlayers.Values)
        {
            list.Clear();
        }

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } playerEnt
                || HasComp<GhostComponent>(playerEnt)
                || TryComp<MobStateComponent>(playerEnt, out var state) && state.CurrentState != MobState.Alive)
                continue;

            var coords = _transform.GetMapCoordinates(playerEnt);
            if (coords.MapId == MapId.Nullspace)
                continue;

            if (!_sleepPlayers.TryGetValue(coords.MapId, out var list))
            {
                list = new List<Vector2>();
                _sleepPlayers[coords.MapId] = list;
            }

            list.Add(coords.Position);
        }

        _sleepQueue.Clear();
        var query = EntityQueryEnumerator<HTNComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _sleepQueue.Add(uid);
        }

        _sleepIndex = 0;
        _sleepBatch = Math.Max(1, (_sleepQueue.Count + SleepPassTicks - 1) / SleepPassTicks);
    }

    private void StepSleepPass()
    {
        if (_sleepIndex >= _sleepQueue.Count)
            return;

        var end = Math.Min(_sleepQueue.Count, _sleepIndex + _sleepBatch);
        for (; _sleepIndex < end; _sleepIndex++)
        {
            CheckSleep(_sleepQueue[_sleepIndex]);
        }
    }

    private void CheckSleep(EntityUid npcUid)
    {
        if (!TryComp<HTNComponent>(npcUid, out var htn) || !TryComp(npcUid, out TransformComponent? npcTransform))
            return;

        if (HasComp<ActorComponent>(npcUid) ||
            (TryComp<MindContainerComponent>(npcUid, out var mindContainer) && mindContainer.HasMind))
            return;

        if (_mobState.IsIncapacitated(npcUid))
            return;

        var hasNearbyPlayer = false;

        var minDistance = htn.SleepPlayerCheckRangeOverride ?? _playerPauseDistance;
        if (htn.SleepMaxGridSpeed is { } ms
            && TryComp<PhysicsComponent>(npcTransform.GridUid, out var gridBody)
            && gridBody.LinearVelocity.Length() > ms)
            hasNearbyPlayer = true;

        if (!hasNearbyPlayer)
        {
            var npcCoords = _transform.GetMapCoordinates(npcUid, npcTransform);
            if (_sleepPlayers.TryGetValue(npcCoords.MapId, out var players))
            {
                var limit = minDistance * minDistance;
                foreach (var pos in players)
                {
                    if ((pos - npcCoords.Position).LengthSquared() <= limit)
                    {
                        hasNearbyPlayer = true;
                        break;
                    }
                }
            }
        }

        var isAwake = IsAwake(npcUid, htn);
        if (!hasNearbyPlayer)
        {
            if (isAwake)
                SleepNPC(npcUid, htn);
        }
        else if (!isAwake)
        {
            WakeNPC(npcUid, htn);
        }
    }
}
