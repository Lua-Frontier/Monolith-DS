using System.Numerics;
using Content.Client.Animations;
using Content.Client.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    private const string FadeAnimationKey = "melee-fade";
    private const string SlashAnimationKey = "melee-slash";
    private const string ThrustAnimationKey = "melee-thrust";

    /// <summary>
    /// Does all of the melee effects for a player that are predicted, i.e. character lunge and weapon animation.
    /// </summary>
    public override void DoLunge(EntityUid user, EntityUid playerUid, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var lunge = GetLungeAnimation(localPos);

        // Stop any existing lunges on the user.
        _animation.Stop(user, MeleeLungeKey);
        _animation.Play(user, lunge, MeleeLungeKey);

        if (localPos == Vector2.Zero || animation == null)
            return;

        if (!_xformQuery.TryGetComponent(user, out var userXform) || userXform.MapID == MapId.Nullspace)
            return;

        var animationUid = Spawn(animation, userXform.Coordinates);

        if (!TryComp<SpriteComponent>(animationUid, out var sprite)
            || !TryComp<WeaponArcVisualsComponent>(animationUid, out var arcComponent))
        {
            return;
        }

// LuaM-start:
        var length = 1f;
        var offset = 1f;
// LuaM-end.

        var spriteRotation = Angle.Zero;
        if (arcComponent.Animation != WeaponArcAnimation.None
            && TryComp(weapon, out MeleeWeaponComponent? meleeWeaponComponent))
        {
            if (user != weapon
                && TryComp(weapon, out SpriteComponent? weaponSpriteComponent))
                _sprite.CopySprite((weapon, weaponSpriteComponent), (animationUid, sprite));

            spriteRotation = meleeWeaponComponent.WideAnimationRotation;
//            var distance = Math.Clamp(localPos.Length() / 2f, 0.2f, 1f); // Commented by LuaM
            if (meleeWeaponComponent.SwingLeft)
                angle *= -1;

// LuaM-start:
            length = 1 / MathF.Max(meleeWeaponComponent.AttackRate, 0.01f) * 0.6f;
            offset = meleeWeaponComponent.AnimationOffset;
// LuaM-end.
        }
        _sprite.SetRotation((animationUid, sprite), localPos.ToWorldAngle());

        var xform = _xformQuery.GetComponent(animationUid);
        TrackUserComponent track;

        switch (arcComponent.Animation)
        {
            case WeaponArcAnimation.Slash:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetSlashAnimation(sprite, angle, spriteRotation, length, offset), SlashAnimationKey); // LuaM: added length, offset
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, length * 0.5f, length + 0.15f), FadeAnimationKey); // LuaM: sprite, 0.065f, 0.065f + 0.05f > sprite, length * 0.5f, length + 0.15f
                break;
            case WeaponArcAnimation.Thrust:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetThrustAnimation((animationUid, sprite), offset, spriteRotation, length), ThrustAnimationKey); // LuaM: removed distance, added length, offset
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, length * 0.5f, length + 0.15f), FadeAnimationKey); // LuaM: sprite, 0.05f, 0.15f > sprite, length * 0.5f, length + 0.15f
                break;
            case WeaponArcAnimation.None:
                var (mapPos, mapRot) = TransformSystem.GetWorldPositionRotation(userXform);
                var worldPos = mapPos + (mapRot - userXform.LocalRotation).RotateVec(localPos);
                var newLocalPos = Vector2.Transform(worldPos, TransformSystem.GetInvWorldMatrix(xform.ParentUid));
                TransformSystem.SetLocalPositionNoLerp(animationUid, newLocalPos, xform);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0f, 0.15f), FadeAnimationKey);
                break;
        }
    }

    private Animation GetSlashAnimation(SpriteComponent sprite, Angle arc, Angle spriteRotation, float length, float offset) // LuaM: added float length, float offset
    {
        var startRotation = sprite.Rotation + arc * 0.5f;
        var endRotation = sprite.Rotation - arc * 0.5f;

        var startRotationOffset = startRotation.RotateVec(new Vector2(0f, -offset * 0.9f));
        var minRotationOffset = sprite.Rotation.RotateVec(new Vector2(0f, -offset * 1.1f));
        var endRotationOffset = endRotation.RotateVec(new Vector2(0f, -offset * 0.9f));

        startRotation += spriteRotation;
        endRotation += spriteRotation;

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length + 0.05f), // LuaM: added + 0.05f
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
//                       new AnimationTrackProperty.KeyFrame(startRotation, 0f), // Commented by LuaM
//                       new AnimationTrackProperty.KeyFrame(startRotation, slashStart), // Commented by LuaM
//                       new AnimationTrackProperty.KeyFrame(endRotation, slashEnd) // Commented by LuaM
// LuaM-start:
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation, endRotation, 0.0f), length * 0.0f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation, endRotation, 0.5f), length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation, endRotation, 1.0f), length * 0.15f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation, endRotation, 0.9f), length * 0.20f),
                        new AnimationTrackProperty.KeyFrame(Angle.Lerp(startRotation, endRotation, 0.8f), length * 0.6f, Easings.OutQuart),
// LuaM-end.
                    },
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
//                        new AnimationTrackProperty.KeyFrame(startRotationOffset, 0f), // Commented by LuaM
//                        new AnimationTrackProperty.KeyFrame(startRotationOffset, slashStart), // Commented by LuaM
//                        new AnimationTrackProperty.KeyFrame(endRotationOffset, slashEnd) // Commented by LuaM
// LuaM-start:
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset, endRotationOffset, 0.0f), length * 0.0f),
                        new AnimationTrackProperty.KeyFrame(minRotationOffset, length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset, endRotationOffset, 1.0f), length * 0.15f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startRotationOffset, endRotationOffset, 0.8f), length * 0.6f, Easings.OutQuart),
// LuaM-end.
                    },
                },
            },
        };
    }

    private Animation GetThrustAnimation(Entity<SpriteComponent> sprite, float offset, Angle spriteRotation, float length) // LuaM: added offset, float length. Removed distance
    {
//        const float thrustEnd = 0.05f; // Commented by LuaM
//        const float length = 0.15f; // Commented by LuaM
//        var startOffset = sprite.Rotation.RotateVec(new Vector2(0f, -distance / 5f)); // Commented by LuaM
//        var endOffset = sprite.Rotation.RotateVec(new Vector2(0f, -distance)); // Commented by LuaM
// LuaM-start:
        var startOffset = Vector2.Zero;
        var endOffset = sprite.Comp.Rotation.RotateVec(new Vector2(0f, -offset * 1.2f));
// LuaM-end.
        _sprite.SetRotation(sprite.AsNullable(), sprite.Comp.Rotation + spriteRotation);

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
//                        new AnimationTrackProperty.KeyFrame(startOffset, 0f), // Commented by LuaM
//                        new AnimationTrackProperty.KeyFrame(endOffset, thrustEnd), // Commented by LuaM
//                        new AnimationTrackProperty.KeyFrame(endOffset, length), // Commented by LuaM
// LuaM-start:
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0f), length * 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.65f), length * 0.10f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 1f), length * 0.20f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.9f), length * 0.30f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Lerp(startOffset, endOffset, 0.7f), length * 0.60f, Easings.OutQuart),
// LuaM-end.
                    },
                },
            },
        };
    }

    private Animation GetFadeAnimation(SpriteComponent sprite, float start, float end)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(end),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color, start),
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(0f), end)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get the sprite offset animation to use for mob lunges.
    /// </summary>
    private Animation GetLungeAnimation(Vector2 direction)
    {
        const float length = 0.1f;

        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
//                        new AnimationTrackProperty.KeyFrame(direction.Normalized() * 0.15f, 0f), // Commented by LuaM
//                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, length) // Commented by LuaM
// LuaM-start:
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(direction.Normalized() * 0.15f, length * 0.4f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, length * 0.6f),
// LuaM-end.
                    }
                }
            }
        };
    }

    /// <summary>
    /// Updates the effect positions to follow the user
    /// </summary>
    private void UpdateEffects()
    {
        var query = EntityQueryEnumerator<TrackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arcComponent, out var xform))
        {
            if (arcComponent.User == null || EntityManager.Deleted(arcComponent.User) || !xform.ParentUid.IsValid()) // LuaM: added || EntityManager.Deleted(arcComponent.User) || !xform.ParentUid.IsValid()) 
                continue;

            Vector2 targetPos = TransformSystem.GetWorldPosition(arcComponent.User.Value);

            if (arcComponent.Offset != Vector2.Zero)
            {
                var entRotation = TransformSystem.GetWorldRotation(xform);
                targetPos += entRotation.RotateVec(arcComponent.Offset);
            }

//            TransformSystem.SetWorldPosition(uid, targetPos); // Commented by LuaM
// LuaM-start:
            var localPos = Vector2.Transform(targetPos, TransformSystem.GetInvWorldMatrix(xform.ParentUid));
            TransformSystem.SetLocalPositionNoLerp(uid, localPos, xform);
// LuaM-end.
        }
    }
}
