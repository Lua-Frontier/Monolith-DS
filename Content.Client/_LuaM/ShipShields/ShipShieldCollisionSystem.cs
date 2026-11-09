using Content.Shared._Crescent.ShipShields;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Client._LuaM.ShipShields;

/// <summary>
/// Mirrors the server rule that a ship shield only collides with projectiles.
/// </summary>
public sealed partial class ShipShieldCollisionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipShieldComponent, PreventCollideEvent>(OnPreventCollide);
    }

    // -- Сервер отменяет столкновения щита со всем, кроме снарядов корабельного оружия, а клиент без этого
    // -- останавливал о щит предсказанные брошенные предметы, и они потом прыгали туда, куда долетели на сервере
    private void OnPreventCollide(Entity<ShipShieldComponent> ent, ref PreventCollideEvent args)
    {
        if (!HasComp<ProjectileComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
