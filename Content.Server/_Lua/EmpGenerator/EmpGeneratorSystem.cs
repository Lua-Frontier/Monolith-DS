using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._NF.Emp.Components;
using Content.Shared.Emp;
using Content.Shared.Tiles;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._Lua.EmpGenerator;

public sealed partial class EmpGeneratorSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private TransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _targets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmpGeneratorComponent, PowerChargeActionEvent>(OnAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EmpGeneratorComponent, PowerChargeComponent>();
        while (query.MoveNext(out var uid, out var generator, out var charge))
        {
            if (!_lights.TryGetLight(uid, out var pointLight))
                continue;

            _lights.SetEnabled(uid, charge.Charge > 0, pointLight);
            _lights.SetRadius(uid, MathHelper.Lerp(generator.LightRadiusMin, generator.LightRadiusMax, charge.Charge), pointLight);
        }
    }

    private void OnAction(Entity<EmpGeneratorComponent> ent, ref PowerChargeActionEvent args)
    {
        var xform = Transform(ent);
        var ownGrid = xform.GridUid;
        var coords = _transform.GetMapCoordinates(ent, xform);

        _targets.Clear();
        _lookup.GetEntitiesInRange(coords.MapId, coords.Position, ent.Comp.Range, _targets);
        foreach (var target in _targets)
        {
            var gridUid = Transform(target).GridUid;
            if (gridUid != null
                && (gridUid == ownGrid
                    || TryComp<ProtectedGridComponent>(gridUid, out var prot) && prot.PreventEmpEvents))
                continue;

            if (ent.Comp.BypassEmpImmunity)
                _emp.DoEmpEffects(target, ent.Comp.EnergyConsumption, ent.Comp.DisableDuration);
            else
                _emp.TryEmpEffects(target, ent.Comp.EnergyConsumption, ent.Comp.DisableDuration);
        }

        var blast = Spawn(ent.Comp.EmpBlastEffect, coords);
        var blastComp = EnsureComp<EmpBlastComponent>(blast);
        blastComp.VisualRange = ent.Comp.Range;
        Dirty(blast, blastComp);

        // Ships far from the generator would not see the blast otherwise.
        if (ent.Comp.Range > _cfg.GetCVar(CVars.NetMaxUpdateRange))
            _pvs.AddGlobalOverride(blast);
    }
}
