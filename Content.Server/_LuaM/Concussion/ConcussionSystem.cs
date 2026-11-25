using Content.Shared._LuaM.Concussion;
using Content.Server._LuaM.Hearing;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._LuaM.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tiles;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Concussion;

/// <summary>
/// Concusses the players around the explosion of a <see cref="ConcussiveExplosiveComponent"/>.
/// The client draws the blur and plays the ringing from <see cref="ConcussedComponent"/>.
/// </summary>
public sealed partial class ConcussionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DeafnessSystem _deafness = default!;

    private const string DeafSource = "Concussion";

    // -- Серия слабых разрывов копится, но не сильнее этого множителя от одного разрыва
    private const float StackLimit = 2f;
    private const float StackFactor = 0.5f;

    // -- Звенеть заново, только если новый разрыв ощутимо сильнее того, что осталось
    private const float SoundStrengthRatio = 0.6f;
    private static readonly TimeSpan SoundCooldown = TimeSpan.FromSeconds(2);

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConcussiveExplosiveComponent, ExplosiveDetonatedEvent>(OnDetonated);
        SubscribeLocalEvent<ConcussiveExplosiveComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<ConcussiveFlashComponent, TriggerEvent>(OnFlashTrigger);
        SubscribeLocalEvent<ConcussionDeafComponent, ComponentShutdown>(OnDeafShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ConcussedComponent>();
        while (query.MoveNext(out var uid, out var concussed))
        {
            if (now >= concussed.EndTime)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<ConcussedComponent>(uid);
        }

        _expired.Clear();

        var deafQuery = EntityQueryEnumerator<ConcussionDeafComponent>();
        while (deafQuery.MoveNext(out var uid, out var deaf))
        {
            if (now >= deaf.EndTime)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<ConcussionDeafComponent>(uid);
        }

        _expired.Clear();
    }

    private void OnDeafShutdown(Entity<ConcussionDeafComponent> ent, ref ComponentShutdown args)
    {
        _deafness.RemoveSource(ent, DeafSource);
    }

    private void OnDetonated(Entity<ConcussiveExplosiveComponent> ent, ref ExplosiveDetonatedEvent args)
    {
        ConcussAround(ent, ent.Comp.Radius ?? ExplosionRadius(args));
    }

    // -- Взрывчатка без своего взрыва (осколочная граната) контузит по срабатыванию
    private void OnTrigger(EntityUid uid, ConcussiveExplosiveComponent comp, TriggerEvent args)
    {
        if (comp.Radius is { } radius && !HasComp<ExplosiveComponent>(uid))
            ConcussAround((uid, comp), radius);
    }

    private void ConcussAround(Entity<ConcussiveExplosiveComponent> ent, float radius)
    {
        var xform = Transform(ent);
        if (xform.GridUid is { } grid && TryComp<ProtectedGridComponent>(grid, out var protectedGrid) && protectedGrid.PreventExplosions)
            return;

        var epicenter = _transform.GetMapCoordinates(ent, xform);
        if (epicenter.MapId == MapId.Nullspace)
            return;

        var comp = ent.Comp;
        var power = Math.Clamp(radius / comp.FullPowerRadius, comp.MinPower, 1f) * comp.PowerMultiplier;
        var range = Math.Min(radius * comp.RangePerRadius + comp.RangeBonus, comp.MaxRange);
        if (power <= 0f || range <= 0f)
            return;

        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var targetXform))
        {
            if (targetXform.MapID != epicenter.MapId || _mobState.IsDead(uid, mobState))
                continue;

            var distance = (_transform.GetWorldPosition(targetXform) - epicenter.Position).Length();
            if (distance >= range)
                continue;

            var strength = Math.Min(power * MathHelper.Lerp(1f, comp.EdgeStrength, distance / range), 1f);
            if (strength < comp.MinStrength)
                continue;

            Concuss(uid, strength, comp.MaxDuration, comp.MinDuration, comp.Sound, blur: true);

            if (comp.DeafRangeFraction > 0f && distance <= range * comp.DeafRangeFraction)
                Deafen(uid, strength, comp);
        }
    }

    // -- Эпицентр: глухота как от берсерка, отдельным источником со своим таймером
    private void Deafen(EntityUid uid, float strength, ConcussiveExplosiveComponent source)
    {
        var duration = source.DeafMaxDuration * strength;
        if (duration < source.DeafMinDuration)
            duration = source.DeafMinDuration;

        var deaf = EnsureComp<ConcussionDeafComponent>(uid);
        var end = _timing.CurTime + duration;
        if (end > deaf.EndTime)
            deaf.EndTime = end;

        _deafness.AddSource(uid, DeafSource, source.DeafSoundVolume, partialSpeech: true);
    }

    // -- Светошумовая: только звон, всем в радиусе вспышки, ослепило их или нет
    private void OnFlashTrigger(EntityUid uid, ConcussiveFlashComponent comp, TriggerEvent args)
    {
        var range = CompOrNull<FlashOnTriggerComponent>(uid)?.Range ?? comp.Range;
        var source = _transform.GetMapCoordinates(uid);
        if (range <= 0f || source.MapId == MapId.Nullspace)
            return;

        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var target, out _, out var mobState, out var targetXform))
        {
            if (targetXform.MapID != source.MapId || _mobState.IsDead(target, mobState))
                continue;

            var distance = (_transform.GetWorldPosition(targetXform) - source.Position).Length();
            if (distance >= range)
                continue;

            var strength = comp.Strength * MathHelper.Lerp(1f, comp.EdgeStrength, distance / range);
            Concuss(target, Math.Min(strength, 1f), comp.MaxDuration, comp.MinDuration, comp.Sound, blur: false);
        }
    }

    private void Concuss(EntityUid uid, float strength, TimeSpan maxDuration, TimeSpan minDuration, SoundSpecifier sound, bool blur)
    {
        var now = _timing.CurTime;
        var concussed = EnsureComp<ConcussedComponent>(uid);
        var current = concussed.GetIntensity(now);

        // -- Вспышка поверх контузии от взрыва её не продлевает и не усиливает замыление, только звенит заново
        if (!blur && current > 0f && concussed.Blur)
        {
            if (strength >= current * SoundStrengthRatio && now - concussed.SoundStart >= SoundCooldown)
            {
                concussed.Sound = sound;
                concussed.SoundStart = now;
                Dirty(uid, concussed);
            }

            return;
        }

        // -- Взрыв поверх звона от вспышки включает замыление
        var newBlur = blur || (current > 0f && concussed.Blur);

        var newStrength = Math.Max(current, Math.Min(strength * StackLimit, strength + current * StackFactor));
        newStrength = Math.Min(newStrength, 1f);
        if (newStrength <= current)
        {
            if (newBlur != concussed.Blur)
            {
                concussed.Blur = newBlur;
                Dirty(uid, concussed);
            }

            return;
        }

        var duration = maxDuration * newStrength;
        if (duration < minDuration)
            duration = minDuration;

        concussed.Strength = newStrength;
        concussed.StartTime = now;
        concussed.EndTime = now + duration;
        concussed.Blur = newBlur;

        if (current <= 0f || (strength >= current * SoundStrengthRatio && now - concussed.SoundStart >= SoundCooldown))
        {
            concussed.Sound = sound;
            concussed.SoundStart = now;
        }

        Dirty(uid, concussed);
    }

    /// <summary>
    /// Same as <c>ExplosionSystem.IntensityToRadius</c>, safe for explosives without a tile intensity cap.
    /// </summary>
    private static float ExplosionRadius(ExplosiveDetonatedEvent args)
    {
        if (args.TotalIntensity <= 0f || args.Slope <= 0f)
            return 0f;

        var simple = MathF.Cbrt(3 * args.TotalIntensity / (args.Slope * MathF.PI));
        if (args.MaxIntensity <= 0f)
            return simple;

        var r0 = args.MaxIntensity / args.Slope;
        var v0 = args.Slope * MathF.PI / 3 * MathF.Pow(r0, 3);
        if (args.TotalIntensity <= v0)
            return simple;

        return r0 * (MathF.Sqrt(12 * args.TotalIntensity / v0 - 3) / 6 + 0.5f);
    }
}
