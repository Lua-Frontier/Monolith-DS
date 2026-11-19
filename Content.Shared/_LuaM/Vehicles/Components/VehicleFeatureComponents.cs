/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._LuaM.Vehicles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VehicleSoundComponent : Component
{
    [DataField]
    public SoundSpecifier? HornSound;

    [DataField]
    public SoundSpecifier? CollisionSound;

    [DataField]
    public float CollisionSoundCooldown = 1.5f;

    [DataField]
    public SoundSpecifier? MobCollisionSound;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextCollisionSound;
}

// -- Двигатель заводит только водитель-игрок; звук мотора клиент собирает из слоёв по скорости
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VehicleEngineComponent : Component
{
    [DataField]
    public SoundSpecifier? StartSound;

    [DataField]
    public SoundSpecifier? StopSound;

    [DataField]
    public SoundSpecifier? IdleSound;

    [DataField]
    public SoundSpecifier? SlowSound;

    [DataField]
    public SoundSpecifier? MediumSound;

    [DataField]
    public SoundSpecifier? FastSound;

    [DataField]
    public SoundSpecifier? SkidStartSound;

    [DataField]
    public SoundSpecifier? SkidSound;

    [DataField]
    public TimeSpan StartDelay = TimeSpan.FromSeconds(1.3);

    [DataField]
    public TimeSpan ExitDelay = TimeSpan.FromSeconds(2.3);

    // -- После глушения гул ещё звучит и плавно затихает
    [DataField]
    public float StopFadeDelay = 0.5f;

    [DataField]
    public float StopFadeTime = 0.5f;

    [DataField]
    public float StartFadeTime = 0.5f;

    [DataField]
    public float Volume = -3f;

    [DataField]
    public float SkidVolume = -2f;

    [DataField]
    public float PitchRange = 0.45f;

    [DataField]
    public float MaxDistance = 18f;

    [DataField]
    public float SkidDegrees = 12f;

    [DataField, AutoNetworkedField]
    public bool EngineOn;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ReadyAt;

    [DataField, AutoNetworkedField]
    public EntityUid? Exiting;

    // -- Водитель не выходит, а пересаживается: после глушения займёт это место
    [DataField, AutoNetworkedField]
    public int? ExitSeat;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExitAt;

    [ViewVariables]
    public EntityUid? StartStream;
}

// -- Препятствие, которое машина сносит на скорости и проезжает насквозь (деревья)
[RegisterComponent]
public sealed partial class VehicleCrushableComponent : Component
{
    [DataField]
    public float MinSpeed = 9f;

    [DataField]
    public float HullDamage = 40f;

    [DataField]
    public float SpeedMultiplier = 0.35f;

    [DataField]
    public EntProtoId? Drop;

    [DataField]
    public SoundSpecifier? Sound;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VehicleOverchargeComponent : Component
{
    [DataField]
    public float SpeedMultiplier = 1.55f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? Sound;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ActiveUntil;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleSpotlightComponent : Component
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public float Energy = 2.5f;

    [DataField]
    public SoundSpecifier? TurnOnSound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");

    [DataField]
    public SoundSpecifier? TurnOffSound = new SoundPathSpecifier("/Audio/Items/flashlight_off.ogg");

    [DataField, AutoNetworkedField]
    public bool Enabled;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleIntegrityComponent : Component
{
    [DataField]
    public float MaxIntegrity = 1000f;

    [DataField, AutoNetworkedField]
    public bool Wrecked;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VehicleDeployableComponent : Component
{
    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan UndeployTime = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public bool Deployed;

    [DataField, AutoNetworkedField]
    public bool Transitioning;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan TransitionEnd;

    [DataField, AutoNetworkedField]
    public EntityUid? Deployer;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleAutoTurretComponent : Component
{
    [DataField]
    public float Range = 10f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.2);

    [ViewVariables]
    public TimeSpan NextTarget;

    [ViewVariables]
    public EntityUid? Target;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleInteriorComponent : Component
{
    [DataField(required: true)]
    public string Sprite = string.Empty;

    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public float FadedAlpha = 0.3f;

    [DataField]
    public float FadeRate = 3f;

    // -- Пиксели от центра кадра кузова для каждого места: кузов нарисован в 3/4, жёсткий поворот мест под него не подходит
    [DataField]
    public Dictionary<Direction, List<Vector2>> SeatOffsets = new();
}

// -- Открытая техника без кузова: седок садится на сиденье по ракурсу, сбоку он над машиной, спереди и сзади под ней
[RegisterComponent]
public sealed partial class VehicleRiderVisualsComponent : Component
{
    [DataField]
    public Dictionary<Direction, List<Vector2>> SeatOffsets = new();

    [DataField]
    public DrawDepth.DrawDepth SideDrawDepth = DrawDepth.DrawDepth.Objects;

    [DataField]
    public DrawDepth.DrawDepth FrontDrawDepth = DrawDepth.DrawDepth.OverMobs;

    // -- Слой над седоками (борта, каркас безопасности): отдельная клиентская сущность
    [DataField]
    public EntProtoId? OverlayVisual;

    // -- При наведении кузов и каркас становятся полупрозрачными, под ними виден интерьер
    [DataField]
    public EntProtoId? InteriorVisual;

    [DataField]
    public float FadedAlpha = 0.3f;

    [DataField]
    public float FadeRate = 3f;
}
