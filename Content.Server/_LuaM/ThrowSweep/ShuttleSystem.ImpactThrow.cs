using System.Numerics;
using Content.Shared._LuaM.Performance;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private Vector2 CapImpactThrow(Vector2 throwVector)
    {
        var max = _cfg.GetCVar(PerformanceCVars.ImpactThrowMax);
        var length = throwVector.Length();
        if (max <= 0f || length <= max)
            return throwVector;

        return throwVector / length * max;
    }
}
