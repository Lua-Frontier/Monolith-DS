// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.SpaceHazards;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Radar;

// Blips on a grid disappear together with the grid on the client, this hides what flies in a veil nebula on its own.
public sealed partial class RadarBlipSystem
{
    [Dependency] private SpaceHazardActivitySystem _hazardActivity = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> _veilFields = new();

    private void CollectVeilFields(MapId mapId)
    {
        _veilFields.Clear();
        foreach (var uid in _hazardActivity.ActiveHazards)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field) ||
                !TryComp(uid, out TransformComponent? xform) ||
                xform.MapID != mapId)
                continue;

            foreach (var weatherId in field.Weathers)
            {
                if (!_prototypes.TryIndex(weatherId, out var weather) || weather.Kind != NebulaWeatherKind.Veil)
                    continue;

                _veilFields.Add((field, _xform.GetWorldPosition(xform)));
                break;
            }
        }
    }

    private bool IsInNebulaVeil(Vector2 worldPos)
    {
        foreach (var (field, fieldPos) in _veilFields)
        {
            if (NebulaVeilHelpers.IsInMidZone(field, fieldPos, worldPos))
                return true;
        }

        return false;
    }
}
