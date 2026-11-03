using Content.Server.Ghost;
using Content.Server.Light.EntitySystems;
using Content.Server.Radio;
using Content.Shared._LuaM.SectorAnomaly;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Robust.Shared.Random;

namespace Content.Server._LuaM.SectorAnomaly;

public sealed partial class SectorDarknessRule : SectorAnomalyRuleSystem<SectorDarknessRuleComponent>
{
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private GhostSystem _ghost = default!;

    private EntityUid? _jammedMap;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    protected override void AnomalyStarted(SectorDarknessRuleComponent component, EntityUid map)
    {
        _jammedMap = map;

        var darkness = EnsureComp<SectorDarknessComponent>(map);
        darkness.Music = component.Music;
        Dirty(map, darkness);
    }

    protected override void AnomalyEnded(SectorDarknessRuleComponent component, EntityUid map)
    {
        _jammedMap = null;
        RemComp<SectorDarknessComponent>(map);
    }

    protected override void AnomalyTick(SectorDarknessRuleComponent component, EntityUid map)
    {
        var lightChance = component.LightBreakChancePerSecond * EffectInterval;
        var flickerChance = component.LightFlickerChancePerSecond * EffectInterval;
        var doorChance = component.DoorToggleChancePerSecond * EffectInterval;

        var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lights.MoveNext(out var uid, out var light, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            if (RobustRandom.Prob(lightChance))
                _poweredLight.TryDestroyBulb(uid, light);
            else if (RobustRandom.Prob(flickerChance))
                _ghost.DoGhostBooEvent(uid);
        }

        var airlocks = EntityQueryEnumerator<AirlockComponent, DoorComponent, TransformComponent>();
        while (airlocks.MoveNext(out var uid, out var airlock, out var door, out var xform))
        {
            if (xform.MapUid == map && airlock.AutoClose && RobustRandom.Prob(doorChance))
                _door.TryToggleDoor(uid, door);
        }
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (_jammedMap is { } map && Transform(args.RadioReceiver).MapUid == map)
            args.Cancelled = true;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _jammedMap = null;
    }
}
