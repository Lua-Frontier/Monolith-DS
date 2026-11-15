// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._Crescent.ShipShields;
using Content.Shared._Lua.SpaceHazards;

namespace Content.Server._Crescent.ShipShields;

// A raised ship shield takes the hits of nebula weather for the hull, the same way it takes projectiles.
public partial class ShipShieldsSystem
{
    private void InitializeNebulaAbsorption()
    {
        SubscribeLocalEvent<ShipShieldedComponent, NebulaShieldHitAttemptEvent>(OnNebulaShieldHit);
    }

    private void OnNebulaShieldHit(Entity<ShipShieldedComponent> ent, ref NebulaShieldHitAttemptEvent args)
    {
        if (args.Absorbed || args.Load <= 0f || ent.Comp.Source is not { } source ||
            !TryComp(source, out ShipShieldEmitterComponent? emitter) || emitter.Recharging)
        {
            return;
        }

        emitter.Damage += args.Load;
        args.Absorbed = true;
    }
}
