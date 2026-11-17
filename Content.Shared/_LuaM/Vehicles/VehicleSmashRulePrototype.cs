/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Vehicles;

// -- Что машина сносит на ходу вместо того, чтобы упереться: правило ловит прототипы по родителям и компонентам/тегам
[Prototype("luamVehicleSmashRule")]
public sealed partial class VehicleSmashRulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int Priority;

    [DataField]
    public List<EntProtoId> Parents = new();

    // -- Читаются только компоненты и теги прототипа
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public List<EntProtoId> ExcludeParents = new();

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public float Damage = 1000f;

    // -- Легче этого класса машина упирается и ничего не ломает
    [DataField]
    public VehicleWeightClass MinDestroyWeightClass = VehicleWeightClass.Weak;

    // -- Легче этого класса машина ломает препятствие, но встаёт; тяжелее — едет дальше
    [DataField]
    public VehicleWeightClass MinContinueWeightClass = VehicleWeightClass.Weak;

    // -- Замедляются только машины легче этого класса
    [DataField]
    public VehicleWeightClass SlowdownBelowWeightClass = VehicleWeightClass.Weak;

    [DataField]
    public float SlowdownMultiplier = 0.5f;

    [DataField]
    public float SlowdownDuration = 0.4f;

    // -- Запитанная дверь держит любую машину: она открывается водителю с доступом
    [DataField]
    public bool RequiresDoorUnpowered;

    [DataField]
    public SoundSpecifier? SmashSound;
}
