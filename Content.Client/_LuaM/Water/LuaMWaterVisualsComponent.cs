/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;

namespace Content.Client._LuaM.Water;

// -- Клиентское состояние погружения, живёт только пока моб в воде или выходит из неё
[RegisterComponent]
public sealed partial class LuaMWaterVisualsComponent : Component
{
    public EntityUid? Water;
    public EntityUid? Splash;
    public float Depth;
    public float TargetDepth;
    public bool Immersed;
    public Vector2 BaseOffset;
    public Vector2 LastOffset;
    public TimeSpan MovingUntil;
    public TimeSpan NextSurfaceCheck;
    public string? SplashState;
    public int SplashSize;
}
