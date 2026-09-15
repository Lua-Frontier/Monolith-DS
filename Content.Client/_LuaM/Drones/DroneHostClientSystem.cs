// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._LuaM.Drones.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Drones;

/// <summary>
/// Displays the first-person-control status icon on a player connected to a remote vehicle.
/// </summary>
public sealed class DroneHostClientSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DroneHostComponent, GetStatusIconsEvent>(OnGetStatusIcon);
    }

    private void OnGetStatusIcon(Entity<DroneHostComponent> ent, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.Add(_prototype.Index(ent.Comp.Icon));
    }
}
