/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._LuaM.Vehicles.Components;

namespace Content.Shared._LuaM.Water;

// -- Вода, в которую стоящие мобы погружаются спрайтом, а колёсная техника вязнет по своему весовому классу
[RegisterComponent]
public sealed partial class LuaMWaterComponent : Component
{
    // -- На сколько пикселей опускается спрайт стоящего в воде
    [DataField]
    public float Depth = 7f;

    // -- Множитель максимальной скорости техники в воде
    [DataField]
    public Dictionary<VehicleWeightClass, float> VehicleSpeedFactors = new();
}
