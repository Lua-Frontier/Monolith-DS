// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server.Radio;
using Content.Server.Shuttles.Events;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.SpaceHazards;

/// <summary>
/// What the weather of a nebula does to the systems of a ship inside it: thrust, FTL and radio.
/// </summary>
public sealed partial class NebulaEnvironmentSystem : EntitySystem
{
    private const int MaxParentChecks = 8;

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, float> _thrustResistance = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnFtlAttempt);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentStartup>(OnThrustResistanceStartup);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, ComponentShutdown>(OnThrustResistanceShutdown);
        SubscribeLocalEvent<NebulaThrustResistanceComponent, EntParentChangedMessage>(OnThrustResistanceMoved);
    }

    /// <summary>
    /// Multiplier of the thrust of a shuttle, below 1 when the weather around it slows it down.
    /// </summary>
    public float GetThrustMultiplier(EntityUid gridUid)
    {
        if (!TryComp(gridUid, out NebulaPresenceComponent? presence))
            return 1f;

        var multiplier = 1f;
        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (_prototypes.TryIndex(weatherId, out var weather))
                multiplier = MathF.Min(multiplier, weather.ThrustMultiplier);
        }

        if (multiplier >= 1f)
            return multiplier;

        return float.Lerp(multiplier, 1f, GetGridThrustResistance(gridUid));
    }

    private void OnThrustResistanceStartup(Entity<NebulaThrustResistanceComponent> ent, ref ComponentStartup args)
    {
        _thrustResistance.Clear();
    }

    private void OnThrustResistanceShutdown(Entity<NebulaThrustResistanceComponent> ent, ref ComponentShutdown args)
    {
        _thrustResistance.Clear();
    }

    private void OnThrustResistanceMoved(Entity<NebulaThrustResistanceComponent> ent, ref EntParentChangedMessage args)
    {
        _thrustResistance.Clear();
    }

    private void OnFtlAttempt(ref ConsoleFTLAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var blocked = HasWeather(args.Uid, weather => weather.BlocksFtl) ||
                      args.Destination is { } destination && IsFtlBlockedAt(_transform.ToMapCoordinates(destination));
        if (!blocked)
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("nebula-ftl-blocked");
    }

    /// <summary>
    /// Whether the point is inside the dense part of a nebula whose weather blocks FTL.
    /// </summary>
    public bool IsFtlBlockedAt(MapCoordinates coordinates)
    {
        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != coordinates.MapId || !field.HasWeather)
                continue;

            if (NebulaVeilHelpers.IsInMidZone(field, _transform.GetWorldPosition(xform), coordinates.Position) &&
                FieldBlocksFtl(field))
                return true;
        }

        return false;
    }

    private bool FieldBlocksFtl(AmbientSpaceFieldComponent field)
    {
        foreach (var weatherId in field.Weathers)
        {
            if (_prototypes.TryIndex(weatherId, out var weather) && weather.BlocksFtl)
                return true;
        }

        return false;
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (IsRadioBlocked(args.RadioSource))
            args.Cancelled = true;
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (IsRadioBlocked(args.RadioSource) || IsRadioBlocked(args.RadioReceiver))
            args.Cancelled = true;
    }

    // A radio is jammed when it or anything that carries it sits on a grid in a radio blackout.
    private bool IsRadioBlocked(EntityUid uid)
    {
        var current = uid;
        for (var i = 0; i < MaxParentChecks && current.Valid; i++)
        {
            if (!TryComp(current, out TransformComponent? xform) || HasComp<NebulaRadioProtectedComponent>(current))
                return false;

            if (HasWeather(current, weather => weather.RadioBlackout))
                return true;

            current = xform.ParentUid;
        }

        return false;
    }

    private bool HasWeather(EntityUid uid, Func<NebulaWeatherPrototype, bool> predicate)
    {
        if (!TryComp(uid, out NebulaPresenceComponent? presence))
            return false;

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (_prototypes.TryIndex(weatherId, out var weather) && predicate(weather))
                return true;
        }

        return false;
    }

    private float GetGridThrustResistance(EntityUid gridUid)
    {
        if (_thrustResistance.TryGetValue(gridUid, out var cached))
            return cached;

        var resistance = 0f;
        var query = EntityQueryEnumerator<NebulaThrustResistanceComponent, TransformComponent>();
        while (query.MoveNext(out _, out var component, out var xform))
        {
            if (xform.GridUid == gridUid)
                resistance = MathF.Max(resistance, component.Resistance);
        }

        resistance = Math.Clamp(resistance, 0f, 1f);
        _thrustResistance[gridUid] = resistance;
        return resistance;
    }
}
