using System.Linq;
using System.Numerics;
using Content.Server._Lua.AmbientSpaceEffects;
using Content.Server._Lua.SpaceHazards;
using Content.Server._NF.Worldgen.Components.Debris;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Nebula;

// -- Погода не исчезает и не появляется, а переезжает: сколько опасных туманностей было в начале раунда, столько и остаётся
public sealed partial class NebulaShiftRuleSystem : GameRuleSystem<NebulaShiftRuleComponent>
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpaceHazardActivitySystem _activity = default!;

    private readonly Dictionary<MapId, List<Entity<AmbientSpaceFieldComponent>>> _dangerous = new();
    private readonly Dictionary<MapId, List<Candidate>> _harmless = new();
    private List<Entity<MapGridComponent>> _grids = new();

    private sealed class Candidate(Entity<AmbientSpaceFieldComponent> field)
    {
        public readonly Entity<AmbientSpaceFieldComponent> Field = field;
        public bool Taken;
    }

    protected override void Started(EntityUid uid, NebulaShiftRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.NextShift = Timing.CurTime + TimeSpan.FromSeconds(component.FirstDelay.Next(RobustRandom));
    }

    protected override void ActiveTick(EntityUid uid, NebulaShiftRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.NextShift is not { } next || Timing.CurTime < next)
            return;

        component.NextShift = Timing.CurTime + TimeSpan.FromSeconds(component.Interval.Next(RobustRandom));
        Shift(component);
    }

    /// <summary>
    /// Смещает туманности сейчас же. Если правило запущено, его следующее смещение откладывается на обычный интервал.
    /// </summary>
    public int ForceShift()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var rule, out _))
        {
            rule.NextShift = Timing.CurTime + TimeSpan.FromSeconds(rule.Interval.Next(RobustRandom));
            return Shift(rule);
        }

        return Shift(new NebulaShiftRuleComponent());
    }

    /// <summary>
    /// Переносит погоду опасных туманностей на безвредные. Возвращает, сколько туманностей сместилось.
    /// </summary>
    private int Shift(NebulaShiftRuleComponent component)
    {
        CollectFields();

        var moved = 0;
        var stayed = 0;
        foreach (var (mapId, dangerous) in _dangerous)
        {
            if (!_harmless.TryGetValue(mapId, out var harmless))
            {
                stayed += dangerous.Count;
                continue;
            }

            RemoveBlocked(component, mapId, harmless);
            RobustRandom.Shuffle(dangerous);
            var count = (int) MathF.Ceiling(dangerous.Count * Math.Clamp(component.Fraction, 0f, 1f));
            for (var i = 0; i < count; i++)
            {
                if (PickTarget(dangerous[i].Comp, harmless) is { } target)
                {
                    MoveWeather(dangerous[i], target.Field);
                    target.Taken = true;
                    moved++;
                }
                else
                {
                    stayed++;
                }
            }
        }

        _dangerous.Clear();
        _harmless.Clear();

        Log.Info($"Nebula shift: {moved} nebulae moved their weather, {stayed} found no place and kept it");
        if (moved > 0 && component.Announcement is { } announcement)
        {
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString(announcement),
                Loc.GetString(component.AnnouncementSender),
                announcementSound: component.AnnouncementSound,
                colorOverride: component.AnnouncementColor);
        }

        return moved;
    }

    private void CollectFields()
    {
        _dangerous.Clear();
        _harmless.Clear();

        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (field.Seed == 0 || xform.MapID == MapId.Nullspace || TerminatingOrDeleted(uid))
                continue;

            if (field.HasWeather)
            {
                if (!_dangerous.TryGetValue(xform.MapID, out var list))
                    _dangerous[xform.MapID] = list = new List<Entity<AmbientSpaceFieldComponent>>();

                list.Add((uid, field));
            }
            else
            {
                if (!_harmless.TryGetValue(xform.MapID, out var list))
                    _harmless[xform.MapID] = list = new List<Candidate>();

                list.Add(new Candidate((uid, field)));
            }
        }
    }

    // -- Как и при генерации, опасная туманность не встаёт на станцию или POI, а корабли игроков не должны оказаться внутри неё
    private void RemoveBlocked(NebulaShiftRuleComponent component, MapId mapId, List<Candidate> candidates)
    {
        candidates.RemoveAll(candidate =>
        {
            var center = _transform.GetWorldPosition(candidate.Field.Owner);
            var radius = MathF.Max(candidate.Field.Comp.Radius, 1f);
            var reach = radius + MathF.Max(component.GridClearance, component.ShipClearance);

            _grids.Clear();
            _map.FindGridsIntersecting(mapId, Box2.CenteredAround(center, new Vector2(reach * 2f)), ref _grids, approx: true, includeMap: false);
            foreach (var grid in _grids)
            {
                if (HasComp<SpaceDebrisComponent>(grid.Owner) || HasComp<NebulaDebrisPieceComponent>(grid.Owner))
                    continue;

                var clearance = HasComp<ShuttleDeedComponent>(grid.Owner) ? component.ShipClearance : component.GridClearance;
                var aabb = grid.Comp.LocalAABB;
                var gridCenter = Vector2.Transform(aabb.Center, _transform.GetWorldMatrix(grid.Owner));
                var gap = (gridCenter - center).Length() - aabb.Size.Length() * 0.5f;
                if (gap < radius + clearance)
                    return true;
            }

            return false;
        });
    }

    // -- Радиационный туман живёт только в больших облаках, а цвет туманности обычно подсказывает её погоду: ищем такую же
    private Candidate? PickTarget(AmbientSpaceFieldComponent source, List<Candidate> candidates)
    {
        var needsBigCloud = source.Weathers.Contains(AmbientSpaceFieldPlacerSystem.RadiationFogWeather);
        var primary = source.Weathers[0];
        var bestScore = -1;
        var best = new List<Candidate>();

        foreach (var candidate in candidates)
        {
            if (candidate.Taken)
                continue;

            var field = candidate.Field.Comp;
            var score = 0;
            if (!needsBigCloud || field.Radius >= AmbientSpaceFieldPlacerSystem.RadiationFogMinRadius)
                score += 2;

            if (ColorWeather(field.Seed) == primary)
                score += 1;

            if (score < bestScore)
                continue;

            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
            }

            best.Add(candidate);
        }

        return best.Count == 0 ? null : RobustRandom.Pick(best);
    }

    private static ProtoId<NebulaWeatherPrototype> ColorWeather(int seed)
    {
        var colors = AmbientSpaceFieldPlacerSystem.WeatherByColorIndex;
        return colors[Math.Clamp(AmbientSpacePalette.ColorIndexFromSeed(seed), 0, colors.Length - 1)];
    }

    private void MoveWeather(Entity<AmbientSpaceFieldComponent> source, Entity<AmbientSpaceFieldComponent> target)
    {
        target.Comp.Weathers.Clear();
        target.Comp.Weathers.AddRange(source.Comp.Weathers);
        source.Comp.Weathers.Clear();
        Dirty(source);
        Dirty(target);

        _activity.SetActivationRange(target.Owner, target.Comp.Radius + AmbientSpaceFieldPlacerSystem.ActivationMargin);
        Log.Debug($"Nebula {ToPrettyString(source)} passed [{string.Join(", ", target.Comp.Weathers.Select(w => w.Id))}] to {ToPrettyString(target)}");
    }
}
