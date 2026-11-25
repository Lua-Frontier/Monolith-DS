using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._LuaM.Concussion;

/// <summary>
/// The creature is concussed by a nearby blast. The effect is strongest right after the blast and wears off by <see cref="EndTime"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause, UnsavedComponent]
public sealed partial class ConcussedComponent : Component
{
    /// <summary>
    /// Part of the effect that holds at full strength before it starts to wear off.
    /// </summary>
    public const float HoldFraction = 0.1f;

    /// <summary>
    /// Strength right after the blast, from 0 to 1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    /// <summary>
    /// Whether the sight blurs. A blast blurs it, a flashbang only rings in the ears.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Blur = true;

    /// <summary>
    /// The ringing in the ears. The client plays it and fades it out together with the blur.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// When the ringing last started, so a barrage does not restart it on every shell.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SoundStart;

    /// <summary>
    /// How strong the effect is at the given moment, from 0 to 1.
    /// </summary>
    public float GetIntensity(TimeSpan now)
    {
        var total = (EndTime - StartTime).TotalSeconds;
        var left = (EndTime - now).TotalSeconds;
        if (total <= 0 || left <= 0)
            return 0f;

        var x = Math.Clamp(left / total / (1 - HoldFraction), 0, 1);
        return Strength * (float) (x * x * (3 - 2 * x));
    }
}
