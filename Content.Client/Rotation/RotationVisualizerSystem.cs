using Content.Shared.Rotation;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Rotation;

public sealed partial class RotationVisualizerSystem : SharedRotationVisualsSystem
{

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private readonly Dictionary<EntityUid, Angle> _rotationTargets = new(); // LuaM

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RotationVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<RotationVisualsComponent, ComponentShutdown>((uid, _, _) => _rotationTargets.Remove(uid)); // LuaM
    }

    private void OnAppearanceChange(EntityUid uid, RotationVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<RotationState>(uid, RotationVisuals.RotationState, out var state, args.Component))
            return;

        switch (state)
        {
            case RotationState.Vertical:
                AnimateSpriteRotation(uid, args.Sprite, component.VerticalRotation, component.AnimationTime);
                break;
            case RotationState.Horizontal:
                AnimateSpriteRotation(uid, args.Sprite, component.HorizontalRotation, component.AnimationTime);
                break;
        }
    }

    /// <summary>
    ///     Rotates a sprite between two animated keyframes given a certain time.
    /// </summary>
    public void AnimateSpriteRotation(EntityUid uid, SpriteComponent spriteComp, Angle rotation, float animationTime)
    {
        if (spriteComp.Rotation.Equals(rotation))
        {
            return;
        }

        var animationComp = EnsureComp<AnimationPlayerComponent>(uid);
        const string animationKey = "rotate";
        // LuaM start
        var running = _animation.HasRunningAnimation(animationComp, animationKey);
        if (running && _rotationTargets.TryGetValue(uid, out var target) && target.Equals(rotation))
            return;

        _rotationTargets[uid] = rotation;
        // LuaM end
        // Stop the current rotate animation and then start a new one
        if (running) // LuaM
        {
            _animation.Stop(animationComp, animationKey);
        }

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(animationTime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(spriteComp.Rotation, 0),
                        new AnimationTrackProperty.KeyFrame(rotation, animationTime)
                    }
                }
            }
        };

        _animation.Play((uid, animationComp), animation, animationKey);
    }
}
