using Content.Shared._LuaM.SectorAnomaly;
using Content.Shared.Light.Components;
using Robust.Shared.Map.Components;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._LuaM.SectorAnomaly;

public sealed partial class SectorBrightnessRule : SectorAnomalyRuleSystem<SectorBrightnessRuleComponent>
{
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    /// <summary>
    /// Clients fade the ambient light out on their own, so the map light is kept a bit longer than that.
    /// </summary>
    private static readonly TimeSpan MapLightRemoveDelay = TimeSpan.FromSeconds(15);

    protected override void AnomalyStarted(SectorBrightnessRuleComponent component, EntityUid map)
    {
        component.AddedMapLight = !HasComp<MapLightComponent>(map);
        EnsureComp<MapLightComponent>(map);

        var brightness = EnsureComp<SectorBrightnessComponent>(map);
        brightness.Music = component.Music;
        brightness.AmbientColor = component.AmbientColor;
        Dirty(map, brightness);

        BoostLights(component, map);
    }

    protected override void AnomalyEnded(SectorBrightnessRuleComponent component, EntityUid map)
    {
        RestoreLights(component);
        RemComp<SectorBrightnessComponent>(map);
        if (!component.AddedMapLight)
            return;

        Timer.Spawn(MapLightRemoveDelay, () =>
        {
            if (!TerminatingOrDeleted(map) && !HasComp<SectorBrightnessComponent>(map))
                RemComp<MapLightComponent>(map);
        });
    }

    private void BoostLights(SectorBrightnessRuleComponent component, EntityUid map)
    {
        if (component.LightEnergyMultiplier == 1f && component.LightSaturationMultiplier == 1f)
            return;

        var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lights.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != map || !_pointLight.TryGetLight(uid, out var light) || !light.Enabled)
                continue;

            var hsv = Color.ToHsv(light.Color);
            hsv.Y = Math.Clamp(hsv.Y * component.LightSaturationMultiplier, 0f, 1f);
            var energy = light.Energy * component.LightEnergyMultiplier;

            component.BoostedLights[uid] = (light.Energy, light.Color, energy);
            _pointLight.SetEnergy(uid, energy, light);
            _pointLight.SetColor(uid, Color.FromHsv(hsv), light);
        }
    }

    private void RestoreLights(SectorBrightnessRuleComponent component)
    {
        foreach (var (uid, (energy, color, boosted)) in component.BoostedLights)
        {
            // Lights that were toggled or replaced in the meantime already have their own values.
            if (!_pointLight.TryGetLight(uid, out var light) || !MathHelper.CloseTo(light.Energy, boosted))
                continue;

            _pointLight.SetEnergy(uid, energy, light);
            _pointLight.SetColor(uid, color, light);
        }

        component.BoostedLights.Clear();
    }
}
