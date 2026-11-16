/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Vehicles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HardpointSlotsComponent : Component
{
    [DataField(required: true)]
    public string Family = string.Empty;

    [DataField(required: true)]
    public List<HardpointSlot> Slots = new();

    [ViewVariables]
    public List<(EntityUid Holder, HardpointSlot Slot, EntityUid? Item)>? SlotCache;
}

[DataDefinition]
public sealed partial class HardpointSlot
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField("hardpointType", required: true)]
    public HardpointType Type;

    [DataField]
    public string? VisualLayer;

    [DataField]
    public EntProtoId? StartingItem;

    [DataField]
    public float InsertDelay = 2f;
}

[Serializable, NetSerializable]
public enum HardpointType : byte
{
    Turret,
    Cannon,
    Launcher,
    RoofAttachment,
    FrontAttachment,
    Wheel,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class HardpointItemComponent : Component
{
    [DataField("hardpointType", required: true)]
    public HardpointType Type;

    [DataField(required: true)]
    public string Family = string.Empty;

    [DataField]
    public string? VisualState;

    // -- Дуло в пикселях от центра спрайта башни для каждого ракурса, ось Y вверх
    [DataField]
    public Dictionary<Direction, Vector2> MuzzleOffsets = new();

    [DataField]
    public string? AmmoType;

    [DataField]
    public float SpotlightRadiusMultiplier = 1f;

    [DataField]
    public float StructureDamageMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleAmmoBoxComponent : Component
{
    [DataField(required: true)]
    public string AmmoType = string.Empty;

    [DataField, AutoNetworkedField]
    public int Amount = 500;

    [DataField]
    public float LoadDelay = 3f;

    [DataField]
    public SoundSpecifier? LoadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/lmg_magin.ogg");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleTurretComponent : Component
{
    [DataField]
    public string TurretSlot = "turret";

    [DataField]
    public float RotationSpeedDegrees = 180f;

    [DataField]
    public float FireGraceDegrees = 2f;

    [DataField]
    public float AimDeadzoneDegrees = 1.5f;

    [DataField]
    public float AimReverseDelay = 0.06f;

    [DataField]
    public bool RequiresDeployed;

    [DataField]
    public Dictionary<Direction, Vector2> PixelOffsets = new();

    [DataField]
    public float MuzzleLength = 0.5f;

    [DataField]
    public string Sprite = "_LuaM/Structures/Vehicles/Humvee/humvee.rsi";

    [DataField, AutoNetworkedField]
    public Angle Rotation;

    [ViewVariables]
    public Vector2? AimOffset;

    [DataField, AutoNetworkedField]
    public HardpointType SelectedWeapon = HardpointType.Cannon;
}

[Serializable, NetSerializable]
public enum VehicleVisuals : byte
{
    Damaged,
    Layer,
}
