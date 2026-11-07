using Content.Shared._LuaM.Throwing;
using Robust.Client.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Throwing;

public sealed partial class PredictedThrowingSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PredictedThrownItemComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PredictedThrownItemComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
    }

    private void OnUpdateIsPredicted(Entity<PredictedThrownItemComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    private void OnStartup(Entity<PredictedThrownItemComponent> ent, ref ComponentStartup args)
    {
        _physics.UpdateIsPredicted(ent.Owner);
    }

    private void OnShutdown(Entity<PredictedThrownItemComponent> ent, ref ComponentShutdown args)
    {
        // -- Предсказание физики выключаем с задержкой, иначе предмет дёрнется назад из-за пинга
        var uid = ent.Owner;
        Timer.Spawn(1000, () =>
        {
            if (!Deleted(uid))
                _physics.UpdateIsPredicted(uid);
        });
    }
}
