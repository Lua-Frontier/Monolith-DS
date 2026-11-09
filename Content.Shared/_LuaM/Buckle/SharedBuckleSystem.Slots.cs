using System.Numerics;
using Content.Shared._LuaM.Buckle;
using Content.Shared.Buckle.Components;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    private Vector2 GetFreeBuckleOffset(Entity<StrapComponent> strap, EntityUid buckle)
    {
        if (!TryComp<StrapSlotsComponent>(strap, out var slots) || slots.Offsets.Count == 0)
            return strap.Comp.BuckleOffset;

        foreach (var offset in slots.Offsets)
        {
            var taken = false;
            foreach (var other in strap.Comp.BuckledEntities)
            {
                if (other == buckle || !TryComp(other, out TransformComponent? otherXform))
                    continue;

                if ((otherXform.LocalPosition - offset).LengthSquared() > 0.01f)
                    continue;

                taken = true;
                break;
            }

            if (!taken)
                return offset;
        }

        return slots.Offsets[0];
    }

    private Vector2 GetBuckleOffset(EntityUid strap, StrapComponent strapComp, Vector2 localPosition)
    {
        if (!TryComp<StrapSlotsComponent>(strap, out var slots) || slots.Offsets.Count == 0)
            return strapComp.BuckleOffset;

        var best = slots.Offsets[0];
        var bestDistance = float.MaxValue;
        foreach (var offset in slots.Offsets)
        {
            var distance = (localPosition - offset).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = offset;
            bestDistance = distance;
        }

        return best;
    }
}
