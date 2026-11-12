// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.AmbientSpaceEffects;

public sealed class AmbientSpaceNebulaVisibility
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypes;
    private TurfSystem? _turf;

    public AmbientSpaceNebulaVisibility(IEntityManager entManager, IPrototypeManager prototypes)
    {
        _entManager = entManager;
        _prototypes = prototypes;
    }

    private TurfSystem Turf => _turf ??= _entManager.System<TurfSystem>();

    /// <summary>
    /// World box that covers every parallax layer of the field as seen from the eye.
    /// </summary>
    public Box2 GetPotentialDrawBounds(AmbientSpaceFieldComponent field, Vector2 fieldPos, Vector2 eyePos, float radius)
    {
        var size = new Vector2(radius * 2f, radius * 2f);
        var bounds = Box2.CenteredAround(fieldPos, size);

        if (!_prototypes.TryIndex(field.Effect, out var effect))
            return bounds;

        bounds = bounds.Union(Box2.CenteredAround(GetParallaxDrawPos(fieldPos, eyePos, effect.LowerParallax), size));
        bounds = bounds.Union(Box2.CenteredAround(GetParallaxDrawPos(fieldPos, eyePos, effect.MidParallax), size));
        bounds = bounds.Union(Box2.CenteredAround(GetParallaxDrawPos(fieldPos, eyePos, effect.UpperParallax), size));
        return bounds;
    }

    public static Vector2 GetParallaxDrawPos(Vector2 fieldPos, Vector2 eyePos, float parallax)
    {
        return fieldPos + (eyePos - fieldPos) * (1f - parallax);
    }

    /// <summary>
    /// The upper layers of a nebula are not drawn over the floor of a ship.
    /// </summary>
    public bool ShouldBlockNebula(TileRef tile)
    {
        return !tile.Tile.IsEmpty && !Turf.IsSpace(tile);
    }
}
