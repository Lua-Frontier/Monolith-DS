/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.ShipShields;

// -- Попадание в корабельный щит: точка на границе в локальных координатах щита и сила вспышки
[Serializable, NetSerializable]
public sealed class ShipShieldImpactEvent(NetEntity shield, Vector2 localPosition, float strength) : EntityEventArgs
{
    public readonly NetEntity Shield = shield;
    public readonly Vector2 LocalPosition = localPosition;
    public readonly float Strength = strength;
}
