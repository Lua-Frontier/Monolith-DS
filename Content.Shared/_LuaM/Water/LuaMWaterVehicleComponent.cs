/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Robust.Shared.Utility;

namespace Content.Shared._LuaM.Water;

// -- Техника в воде не опускается спрайтом (иначе разъедутся сиденья, каркас и турель): поверх корпуса ложится пена по его контуру
[RegisterComponent]
public sealed partial class LuaMWaterVehicleComponent : Component
{
    // -- 4-направленное состояние того же размера, что и кузов, обведённое по силуэту кузова с колёсами
    [DataField(required: true)]
    public SpriteSpecifier.Rsi Foam = default!;
}
