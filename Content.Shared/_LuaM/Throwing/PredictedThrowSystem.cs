using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Containers;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Throwing;

public sealed partial class PredictedThrowSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ThrowItemInHand, new PointerInputCmdHandler(HandleThrowItem))
            .Register<PredictedThrowSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<PredictedThrowSystem>();
    }

    private bool HandleThrowItem(ICommonSession? session, EntityCoordinates coordinates, EntityUid entity)
    {
        if (session?.AttachedEntity is not { Valid: true } player || !Exists(player) || !coordinates.IsValid(EntityManager))
            return false;

        ThrowHeldItem(player, coordinates);
        // -- Всегда false, чтобы ввод ушёл на сервер и бросок там подтвердился
        return false;
    }

    public bool ThrowHeldItem(EntityUid player, EntityCoordinates coordinates, float minDistance = 0.1f)
    {
        if (_container.IsEntityInContainer(player) ||
            !TryComp(player, out HandsComponent? hands) ||
            hands.ActiveHandEntity is not { } throwEnt ||
            !_actionBlocker.CanThrow(player, throwEnt))
            return false;

        var stack = CompOrNull<StackComponent>(throwEnt);
        var split = stack is { Count: > 1, ThrowIndividually: true };

        // -- Бросок того, кого несёшь, и деление стопки клиент не предскажет: их обрабатывает только сервер
        if (_net.IsClient && (split || HasComp<VirtualItemComponent>(throwEnt)))
            return false;

        if (_timing.CurTime < hands.NextThrowTime)
            return false;

        hands.NextThrowTime = _timing.CurTime + hands.ThrowCooldown;
        Dirty(player, hands);

        if (split)
        {
            if (_stack.Split(throwEnt, 1, Transform(player).Coordinates, stack) is not { Valid: true } splitStack)
                return false;

            throwEnt = splitStack;
        }

        var direction = _transform.ToMapCoordinates(coordinates).Position - _transform.GetWorldPosition(player);
        if (direction == Vector2.Zero)
            return true;

        var length = direction.Length();
        var distance = Math.Clamp(length, minDistance, hands.ThrowRange);
        direction *= distance / length;

        var ev = new BeforeThrowEvent(throwEnt, direction, hands.BaseThrowspeed, player);
        RaiseLocalEvent(player, ref ev);

        if (ev.Cancelled)
            return true;

        // -- После сброса предсказания руки клиента ещё "держат" предмет, который клиент уже выбросил из контейнера:
        // -- TryDrop тогда падает, бросок обрывается до импульса, и предмет лежит у ног, пока не придёт сервер
        if (_hands.IsHolding(player, throwEnt, out _, hands)
            && _container.IsEntityInContainer(throwEnt)
            && !_hands.TryDrop(player, throwEnt, handsComp: hands))
            return false;

        _throwing.TryThrow(ev.ItemUid,
            ev.Direction,
            ev.ThrowSpeed,
            ev.PlayerUid,
            compensateFriction: !HasComp<LandAtCursorComponent>(ev.ItemUid) && !AimedAtThrowInsert(coordinates),
            predicted: true);

        return true;
    }

    // -- С компенсацией трения предмет приземляется на 80% пути и въезжает в мусоропровод уже не брошенным
    private bool AimedAtThrowInsert(EntityCoordinates coordinates)
    {
        var mapCoords = _transform.ToMapCoordinates(coordinates);
        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, mapCoords));
        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<ThrowInsertContainerComponent>(ent))
                return true;
        }

        return false;
    }
}
