using Content.Server._Mono.ChimeraFloorCleaner;
using Content.Server.Spreader;
using Content.Shared._Crescent.ShipShields;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Spreader;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.BioShield;

public sealed partial class BioShieldSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1.5);
    private const float WitherChance = 0.6f;

    private readonly Dictionary<EntityUid, DamageSpecifier> _shieldedGrids = new();
    private TimeSpan _nextSweep;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChimeraCleanableComponent, MapInitEvent>(OnBiomassMapInit);
    }

    private void OnBiomassMapInit(Entity<ChimeraCleanableComponent> ent, ref MapInitEvent args)
    {
        if (Transform(ent).GridUid is not { } grid)
            return;

        CollectShieldedGrids();
        if (_shieldedGrids.ContainsKey(grid))
            QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextSweep)
            return;

        _nextSweep = _timing.CurTime + SweepInterval;

        CollectShieldedGrids();
        if (_shieldedGrids.Count == 0)
            return;

        var query = EntityQueryEnumerator<ChimeraCleanableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is not { } grid || !_shieldedGrids.TryGetValue(grid, out var damage))
                continue;

            // -- Плоть увядает по стадиям роста в случайном порядке, а не исчезает вся разом
            if (!_random.Prob(WitherChance))
                continue;

            if (TryComp<KudzuComponent>(uid, out var kudzu) && kudzu.GrowthLevel > 1)
            {
                RemCompDeferred<GrowingKudzuComponent>(uid);
                kudzu.GrowthLevel--;
                _appearance.SetData(uid, KudzuVisuals.GrowthLevel, kudzu.GrowthLevel);
                continue;
            }

            _damageable.TryChangeDamage(uid, damage, true);
        }
    }

    private void CollectShieldedGrids()
    {
        _shieldedGrids.Clear();

        var query = EntityQueryEnumerator<BioShieldComponent, ShipShieldEmitterComponent, TransformComponent>();
        while (query.MoveNext(out _, out var bio, out var emitter, out var xform))
        {
            // -- Только пока щит поднят: при перегрузке или без питания плоть снова может расти
            if (emitter.Shield == null)
                continue;

            if ((emitter.Shielded ?? xform.GridUid) is { } grid)
                _shieldedGrids[grid] = bio.Damage;
        }
    }
}
