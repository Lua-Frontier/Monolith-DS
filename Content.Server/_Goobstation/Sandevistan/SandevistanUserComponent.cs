using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Goobstation.Sandevistan;
using Content.Shared.Abilities;
using Content.Shared.Actions;
using Content.Shared.Damage; // LuaM
using Robust.Shared.Audio;

// Ideally speaking this should be on the heart itself... but this also works.
namespace Content.Server._Goobstation.Sandevistan;

[RegisterComponent]
public sealed partial class SandevistanUserComponent : Component
{
    /// <summary>
    /// Marker component indicating that the Sandevistan is currently active.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ActiveSandevistanUserComponent? Active;

    // LuaM start: the Sandevistan no longer shuts down by itself, it starts hurting the user instead
    /// <summary>
    /// Time when the safe running time of the active Sandevistan ends.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? OverloadAt;

    /// <summary>
    /// Time of the next overload damage tick.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextOverloadDamage;

    /// <summary>
    /// Whether the user was already warned about the overload during this activation.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool OverloadWarned;

    /// <summary>
    /// Whether the user was already warned that the safe running time is about to end.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool PreOverloadWarned;

    /// <summary>
    /// How long before the end of the safe running time the user gets warned.
    /// </summary>
    [DataField]
    public TimeSpan OverloadWarningTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Sound played to the user together with the warnings.
    /// </summary>
    [DataField]
    public SoundSpecifier? OverloadWarningSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");

    /// <summary>
    /// Strength of the glitch screen effect during the safe running time.
    /// </summary>
    [DataField]
    public float GlitchIntensity = 0.4f;

    /// <summary>
    /// Strength of the glitch screen effect once the Sandevistan is overloaded.
    /// </summary>
    [DataField]
    public float OverloadGlitchIntensity = 1f;

    /// <summary>
    /// Damage dealt every <see cref="OverloadInterval"/> after the safe running time.
    /// </summary>
    [DataField]
    public DamageSpecifier OverloadDamage = new()
    {
        DamageDict = new() { { "Cellular", 15 } },
    };

    [DataField]
    public TimeSpan OverloadInterval = TimeSpan.FromSeconds(1);

    // LuaM-start
    /// <summary>
    /// If set, dealt instead of <see cref="OverloadDamage"/> once the implant has been running
    /// for <see cref="HeavyOverloadAfter"/>.
    /// </summary>
    [DataField]
    public DamageSpecifier? HeavyOverloadDamage;

    [DataField]
    public TimeSpan HeavyOverloadAfter = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Shown to the user <see cref="OverloadWarningTime"/> before the heavy damage starts. No warning if null.
    /// </summary>
    [DataField]
    public LocId? HeavyOverloadWarning;

    [ViewVariables]
    public bool HeavyOverloadWarned;

    /// <summary>
    /// When the implant was turned on.
    /// </summary>
    [ViewVariables]
    public TimeSpan ActivatedAt;
    // LuaM-end
    // LuaM end

    /// <summary>
    /// Time when the Sandevistan will finish recharging and become usable again.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? RechargeAt;

    /// <summary>
    /// How long the Sandevistan can run safely after activation, after that it deals <see cref="OverloadDamage"/>.
    /// Configured by the implant's YAML prototype.
    /// </summary>
    [DataField]
    public TimeSpan SafeDuration = TimeSpan.FromSeconds(10); // LuaM: ActiveDuration 8 > SafeDuration 10

    /// <summary>
    /// How long the Sandevistan must recharge after being disabled.
    /// Configured by the implant's YAML prototype.
    /// </summary>
    [DataField]
    public TimeSpan RechargeDuration = TimeSpan.FromSeconds(40);

    [DataField]
    public string ActionProto = "ActionToggleSandevistan";

    // LuaM-start
    /// <summary>
    /// Whether guns are blocked while running. The Kerenzikov lets its user shoot.
    /// </summary>
    [DataField]
    public bool BlockGuns = true;

    /// <summary>
    /// Whether the user gets the "safe mode ends" and "overloaded" warnings.
    /// Off for implants that hurt their user the whole time they run.
    /// </summary>
    [DataField]
    public bool OverloadWarnings = true;
    // LuaM-end

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;

    [DataField]
    public float MovementSpeedModifier = 2f;

    [DataField]
    public float AttackSpeedModifier = 2f;

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/_Goobstation/Misc/sande_start.ogg");

    [DataField]
    public SoundSpecifier? EndSound = new SoundPathSpecifier("/Audio/_Goobstation/Misc/sande_end.ogg");

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? RunningSound;

    [ViewVariables(VVAccess.ReadOnly)]
    public DogVisionComponent? Overlay;

    [ViewVariables(VVAccess.ReadOnly)]
    public TrailComponent? Trail;

    [ViewVariables(VVAccess.ReadWrite)]
    public int ColorAccumulator = 0;
}