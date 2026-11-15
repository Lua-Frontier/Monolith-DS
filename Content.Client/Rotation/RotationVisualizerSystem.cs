using Content.Client.DamageState; // LuaM
using Content.Shared.Rotation;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Rotation;

public sealed partial class RotationVisualizerSystem : SharedRotationVisualsSystem
{

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SpriteSystem _sprite = default!; // LuaM

    private readonly Dictionary<EntityUid, Angle> _rotationTargets = new(); // LuaM

    // LuaM-start
    private const int LyingDrawDepth = (int) Shared.DrawDepth.DrawDepth.Items;
    private readonly Dictionary<EntityUid, int> _standingDrawDepths = new();
    // LuaM-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RotationVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<RotationVisualsComponent, ComponentShutdown>((uid, _, _) =>
        {
            _rotationTargets.Remove(uid); // LuaM
            _standingDrawDepths.Remove(uid); // LuaM
        });
    }

    // LuaM-start
    private void UpdateLyingDrawDepth(EntityUid uid, SpriteComponent sprite, bool lying)
    {
        if (lying)
        {
            if (sprite.DrawDepth <= LyingDrawDepth)
                return;

            _standingDrawDepths.TryAdd(uid, sprite.DrawDepth);
            _sprite.SetDrawDepth((uid, sprite), LyingDrawDepth);
            return;
        }

        if (!_standingDrawDepths.Remove(uid, out var original))
            return;

        if (sprite.DrawDepth == LyingDrawDepth)
            _sprite.SetDrawDepth((uid, sprite), original);
        else if (TryComp<DamageStateVisualsComponent>(uid, out var damageVisuals) && damageVisuals.OriginalDrawDepth == LyingDrawDepth)
            damageVisuals.OriginalDrawDepth = original;
    }
    // LuaM-end

    private void OnAppearanceChange(EntityUid uid, RotationVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<RotationState>(uid, RotationVisuals.RotationState, out var state, args.Component))
            return;

        UpdateLyingDrawDepth(uid, args.Sprite, state == RotationState.Horizontal); // LuaM

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
            _animation.Stop((uid, animationComp), animationKey);
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
