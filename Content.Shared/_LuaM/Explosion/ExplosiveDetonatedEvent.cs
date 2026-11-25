namespace Content.Shared._LuaM.Explosion;

/// <summary>
/// Raised on an explosive right after it detonated, while it still exists.
/// </summary>
[ByRefEvent]
public readonly record struct ExplosiveDetonatedEvent(float TotalIntensity, float Slope, float MaxIntensity);
