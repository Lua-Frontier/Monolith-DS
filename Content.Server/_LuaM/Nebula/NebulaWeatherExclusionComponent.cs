namespace Content.Server._LuaM.Nebula;

/// <summary>
/// Вешается на грид станции: рядом с ней опасная туманность не появляется при генерации
/// и не получает погоду при смещении туманностей.
/// </summary>
[RegisterComponent, Access(typeof(NebulaWeatherExclusionSystem))]
public sealed partial class NebulaWeatherExclusionComponent : Component
{
    /// <summary>
    /// Сколько метров должно быть от края грида до края опасной туманности.
    /// </summary>
    [DataField]
    public float Clearance = 2000f;
}
