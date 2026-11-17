using Content.Shared.Throwing;

namespace Content.Shared._LuaM.Throwing;

// -- Иначе ThrownItem ссылается на удалённого бросающего, и каждая отправка состояния пишет ошибку.
public sealed class ThrowerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrowerComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public void Track(EntityUid thrower, EntityUid item)
    {
        var comp = EnsureComp<ThrowerComponent>(thrower);
        comp.InFlight.RemoveWhere(old => !TryComp<ThrownItemComponent>(old, out var thrown) || thrown.Thrower != thrower);
        comp.InFlight.Add(item);
    }

    private void OnTerminating(Entity<ThrowerComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var item in ent.Comp.InFlight)
        {
            if (!TryComp<ThrownItemComponent>(item, out var thrown) || thrown.Thrower != ent.Owner)
                continue;

            thrown.Thrower = null;
            Dirty(item, thrown);
        }
    }
}
