using Content.Shared.Interaction;

namespace Content.Shared.MouseRotator;

/// <summary>
/// This handles rotating an entity based on mouse location
/// </summary>
/// <see cref="MouseRotatorComponent"/>
public abstract partial class SharedMouseRotatorSystem : EntitySystem
{
    [Dependency] private RotateToFaceSystem _rotate = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<RequestMouseRotatorRotationEvent>(OnRequestRotation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // TODO maybe `ActiveMouseRotatorComponent` to avoid querying over more entities than we need?
        // (if this is added to players)
        // (but arch makes these fast anyway, so)
        var query = EntityQueryEnumerator<MouseRotatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var rotator, out var xform))
        {
            if (rotator.GoalRotation == null)
                continue;

            if (_rotate.TryRotateTo(
                    uid,
                    rotator.GoalRotation.Value,
                    frameTime,
                    rotator.AngleTolerance,
                    MathHelper.DegreesToRadians(rotator.RotationSpeed),
                    xform))
            {
                // Stop rotating if we finished
                rotator.GoalRotation = null;
                Dirty(uid, rotator);
            }
        }
    }

    private void OnRequestRotation(RequestMouseRotatorRotationEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ent
            || !TryComp<MouseRotatorComponent>(ent, out var rotator))
        {
            // -- Клиент шлёт поворот, пока до него не дошло состояние с удалённым MouseRotator (рассинхрон предсказания боевого режима)
            Log.Debug($"User {args.SenderSession.Name} ({args.SenderSession.UserId}) tried setting local rotation directly without a valid mouse rotator component attached!"); // LuaM: Error > Debug
            return;
        }

        rotator.GoalRotation = msg.Rotation;
        Dirty(ent, rotator);
    }
}
