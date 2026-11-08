using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using JetBrains.Annotations;

// Leaving this one in the default namespace because I am afraid to test it 
// in the Shitmed namespace lmao.
namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class GibPartBehavior : IThresholdBehavior
{
    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        if (!system.EntityManager.TryGetComponent(owner, out BodyPartComponent? part))
            return;

        // -- Конечность отрубается почти у порога гиба, и первый же урон по ней на полу удалял её без гибов
        if (part.Body == null) // LuaM
            return; // LuaM

        system.BodySystem.GibPart(owner, part);
    }
}

