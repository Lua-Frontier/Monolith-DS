using Content.Server.Administration;
using Content.Server.Machines.EntitySystems;
using Content.Server.ParticleAccelerator.Components;
using Content.Server.ParticleAccelerator.EntitySystems;
using Content.Server.Singularity.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Machines.Components;
using Content.Shared.Singularity.Components;
using Content.Shared.Tag;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.Singularity
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed partial class StartSingularityEngineCommand : LocalizedEntityCommands
    {
        [Dependency] private EmitterSystem _emitterSystem = default!;
        [Dependency] private MultipartMachineSystem _multipartSystem = default!;
        [Dependency] private ParticleAcceleratorSystem  _paSystem = default!;
        [Dependency] private RadiationCollectorSystem _radCollectorSystem = default!;
        [Dependency] private IPrototypeManager _prototype = default!; // LuaM
        [Dependency] private IComponentFactory _componentFactory = default!; // LuaM

        public override string Command => "startsingularityengine";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 0)
            {
                shell.WriteLine(Loc.GetString($"shell-need-exactly-zero-arguments"));
                return;
            }

            // LuaM-start
            var fieldTags = new Dictionary<EntityUid, HashSet<string>>();
            var generatorQuery = EntityManager.EntityQueryEnumerator<ContainmentFieldGeneratorComponent, TransformComponent>();
            while (generatorQuery.MoveNext(out _, out var generator, out var generatorXform))
            {
                if (generatorXform.GridUid is { } generatorGrid)
                    fieldTags.GetOrNew(generatorGrid).Add(generator.IDTag);
            }

            // Turn on emitters
            var emitterQuery = EntityManager.EntityQueryEnumerator<EmitterComponent, TransformComponent>();
            while (emitterQuery.MoveNext(out var uid, out var emitterComponent, out var emitterXform))
            {
                if (emitterXform.GridUid is not { } grid ||
                    !fieldTags.TryGetValue(grid, out var tags) ||
                    !_prototype.TryIndex<EntityPrototype>(emitterComponent.BoltType, out var bolt) ||
                    !bolt.TryGetComponent<TagComponent>(out var boltTags, _componentFactory) ||
                    !boltTags.Tags.Any(tag => tags.Contains(tag)))
                {
                    continue;
                }

                _emitterSystem.SwitchOn(uid, emitterComponent);
            }
            // LuaM-end

            // Turn on radiation collectors
            var radiationCollectorQuery = EntityManager.EntityQueryEnumerator<RadiationCollectorComponent>();
            while (radiationCollectorQuery.MoveNext(out var uid, out var radiationCollectorComponent))
            {
                _radCollectorSystem.SetCollectorEnabled(uid, enabled: true, user: null, radiationCollectorComponent);
            }

            // Setup PA
            var paQuery = EntityManager.EntityQueryEnumerator<ParticleAcceleratorControlBoxComponent>();
            while (paQuery.MoveNext(out var paId, out var paControl))
            {
                if (!EntityManager.TryGetComponent<MultipartMachineComponent>(paId, out var machine))
                    continue;

                if (!_multipartSystem.Rescan((paId, machine)))
                    continue;

                _paSystem.SetStrength(paId, ParticleAcceleratorPowerState.Level0, comp: paControl);
                _paSystem.SwitchOn(paId, comp: paControl);
            }

            shell.WriteLine(Loc.GetString($"shell-command-success"));
        }
    }
}
