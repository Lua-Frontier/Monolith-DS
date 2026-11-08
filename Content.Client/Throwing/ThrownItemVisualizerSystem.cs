using System.Linq; // LuaM
using System.Numerics; // LuaM
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

    // LuaM start
    // -- Предсказанный бросок анимируется один раз: состояния сервера и перепредсказание приходят с тем же ThrownTime.
    // -- Исходный масштаб хранится здесь, а не в компоненте: после сброса предсказания компонент полёта приходит новым,
    // -- и оборванная на увеличенном спрайте анимация иначе становилась новой базой, предмет рос с каждым броском
    private readonly Dictionary<EntityUid, (TimeSpan? ThrownTime, Vector2 BaseScale)> _animated = new();
    // LuaM end

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
        if (_animated.TryGetValue(uid, out var animated) && animated.ThrownTime == component.ThrownTime
            || component.LandTime is { } land && land <= _timing.CurTime)
            return;
        // LuaM end

        var animationPlayer = EnsureComp<AnimationPlayerComponent>(uid);

        if (_anim.HasRunningAnimation(uid, animationPlayer, AnimationKey))
            return;

        var baseScale = _animated.TryGetValue(uid, out var previous) ? previous.BaseScale : sprite.Scale; // LuaM
        var anim = GetAnimation(component, baseScale); // LuaM: (uid, component, sprite) > component, baseScale
        if (anim == null)
            return;

        // LuaM start
        if (_animated.Count > 256)
        {
            foreach (var stale in _animated.Keys.Where(e => !Exists(e)).ToList())
                _animated.Remove(stale);
        }

        _animated[uid] = (component.ThrownTime, baseScale);
        // LuaM end
        _anim.Play((uid, animationPlayer), anim, AnimationKey);
    }

    private void OnShutdown(EntityUid uid, ThrownItemComponent component, ComponentShutdown args)
    {
        // LuaM start
        if (_timing.ApplyingState && _animated.ContainsKey(uid) && component.LandTime > _timing.CurTime)
            return;

        if (_animated.Remove(uid, out var animated) && TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetScale((uid, sprite), animated.BaseScale);

        if (_anim.HasRunningAnimation(uid, AnimationKey))
            _anim.Stop(uid, AnimationKey);
        // LuaM end
    }

    private static Animation? GetAnimation(ThrownItemComponent component, Vector2 scale) // LuaM: Entity<ThrownItemComponent, SpriteComponent> > component, scale
    {
        if (component.LandTime - component.ThrownTime is not { } length)
            return null;

        if (length <= TimeSpan.Zero)
            return null;

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
