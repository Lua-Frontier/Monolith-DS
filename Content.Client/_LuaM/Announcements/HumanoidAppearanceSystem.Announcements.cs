// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.Humanoid;

public sealed partial class HumanoidAppearanceSystem
{
    /// <summary>Rebuild a local announcement doll after restoring its full appearance snapshot.</summary>
    public void RefreshAnnouncementAppearance(Entity<HumanoidAppearanceComponent> doll)
    {
        if (!IsClientSide(doll.Owner))
            return;
        UpdateSprite(doll.Comp, (doll.Owner, Comp<SpriteComponent>(doll.Owner)));
    }
}
