using System.Linq;
using Content.Shared._LuaM.Cyberware;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;

namespace Content.Server._LuaM.Cyberware;

public sealed partial class TraitOrganForEachPartSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitOrganForEachPartComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<TraitOrganForEachPartComponent> ent, ref MapInitEvent args)
    {
        foreach (var (partId, partComp) in _body.GetBodyChildrenOfType(ent, ent.Comp.PartType).ToList())
        {
            var organ = Spawn(ent.Comp.Organ, Transform(ent).Coordinates);
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || !_body.CanInsertOrgan(partId, organComp.SlotId, partComp))
            {
                Del(organ);
                continue;
            }

            if (ent.Comp.Replace)
            {
                foreach (var (oldId, oldComp) in _body.GetPartOrgans(partId, partComp).ToList())
                {
                    if (oldComp.SlotId != organComp.SlotId || !_body.RemoveOrgan(oldId, oldComp))
                        continue;

                    QueueDel(oldId);
                }
            }

            if (!_body.InsertOrgan(partId, organ, organComp.SlotId, partComp, organComp))
                Del(organ);
        }

        RemCompDeferred<TraitOrganForEachPartComponent>(ent);
    }
}
