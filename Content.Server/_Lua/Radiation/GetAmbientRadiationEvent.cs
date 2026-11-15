namespace Content.Server._Lua.Radiation;

/// <summary>
/// Raised on a radiation receiver to add radiation that comes from its surroundings and not from a source entity.
/// </summary>
[ByRefEvent]
public record struct GetAmbientRadiationEvent(float Radiation = 0f);
