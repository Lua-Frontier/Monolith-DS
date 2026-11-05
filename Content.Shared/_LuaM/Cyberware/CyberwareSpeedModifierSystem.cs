using Content.Shared.Movement.Systems;

namespace Content.Shared._LuaM.Cyberware;

public sealed partial class CyberwareSpeedModifierSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberwareSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<CyberwareSpeedModifierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyberwareSpeedModifierComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyberwareSpeedModifierComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnRefresh(Entity<CyberwareSpeedModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // the shutdown refresh must not count the component that is going away
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.ModifySpeed(ent.Comp.WalkModifier, ent.Comp.SprintModifier);
    }

    private void OnStartup(EntityUid uid, CyberwareSpeedModifierComponent comp, ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnShutdown(EntityUid uid, CyberwareSpeedModifierComponent comp, ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnState(Entity<CyberwareSpeedModifierComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }
}
