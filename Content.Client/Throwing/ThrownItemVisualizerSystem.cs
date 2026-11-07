using System.Linq; // LuaM
using Content.Shared.Throwing;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Timing; // LuaM

namespace Content.Client.Throwing;

/// <summary>
///     Handles animating thrown items.
/// </summary>
public sealed partial class ThrownItemVisualizerSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _anim = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!; // LuaM

    private const string AnimationKey = "thrown-item";

    private readonly Dictionary<EntityUid, TimeSpan?> _animatedThrows = new(); // LuaM

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrownItemComponent, AfterAutoHandleStateEvent>(OnAutoHandleState);
        SubscribeLocalEvent<ThrownItemComponent, ComponentStartup>(OnStartup); // LuaM
        SubscribeLocalEvent<ThrownItemComponent, ComponentShutdown>(OnShutdown);
    }

    // LuaM start
    private void OnStartup(EntityUid uid, ThrownItemComponent component, ComponentStartup args)
    {
        if (_timing.IsFirstTimePredicted && !_timing.ApplyingState)
            TryAnimate(uid, component);
    }
    // LuaM end

    private void OnAutoHandleState(EntityUid uid, ThrownItemComponent component, ref AfterAutoHandleStateEvent args)
    {
        TryAnimate(uid, component); // LuaM
    }

    private void TryAnimate(EntityUid uid, ThrownItemComponent component) // LuaM
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !component.Animate)
            return;

        // LuaM start
        // -- Предсказанный бросок уже анимирован; повторные состояния от сервера и перепредсказание не должны запускать его снова
        if (_animatedThrows.TryGetValue(uid, out var animated) && animated == component.ThrownTime
            || component.LandTime is { } land && land <= _timing.CurTime)
            return;
        // LuaM end

        var animationPlayer = EnsureComp<AnimationPlayerComponent>(uid);

        if (_anim.HasRunningAnimation(uid, animationPlayer, AnimationKey))
            return;

        var anim = GetAnimation((uid, component, sprite));
        if (anim == null)
            return;

        component.OriginalScale = sprite.Scale;
        // LuaM start
        if (_animatedThrows.Count > 256)
        {
            foreach (var stale in _animatedThrows.Keys.Where(e => !Exists(e)).ToList())
                _animatedThrows.Remove(stale);
        }

        _animatedThrows[uid] = component.ThrownTime;
        // LuaM end
        _anim.Play((uid, animationPlayer), anim, AnimationKey);
    }

    private void OnShutdown(EntityUid uid, ThrownItemComponent component, ComponentShutdown args)
    {
        if (_timing.ApplyingState && _animatedThrows.ContainsKey(uid) && component.LandTime > _timing.CurTime) // LuaM
            return; // LuaM

        if (!_anim.HasRunningAnimation(uid, AnimationKey))
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite) && component.OriginalScale != null)
            _sprite.SetScale((uid, sprite), component.OriginalScale.Value);

        _anim.Stop(uid, AnimationKey);
    }

    private static Animation? GetAnimation(Entity<ThrownItemComponent, SpriteComponent> ent)
    {
        if (ent.Comp1.LandTime - ent.Comp1.ThrownTime is not { } length)
            return null;

        if (length <= TimeSpan.Zero)
            return null;

        var scale = ent.Comp2.Scale;
        var lenFloat = (float) length.TotalSeconds;

        // TODO use like actual easings here
        return new Animation
        {
            Length = length,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(scale, 0.0f),
                        new AnimationTrackProperty.KeyFrame(scale * 1.4f, lenFloat * 0.25f),
                        new AnimationTrackProperty.KeyFrame(scale, lenFloat * 0.75f)
                    },
                    InterpolationMode = AnimationInterpolationMode.Linear
                }
            }
        };
    }
}
