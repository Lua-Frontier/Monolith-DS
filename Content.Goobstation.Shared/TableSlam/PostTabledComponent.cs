// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Shared.TableSlam;

/// <summary>
/// The entity was just slammed onto a table, a shove can stun them.
/// </summary>
[RegisterComponent]
public sealed partial class PostTabledComponent : Component
{
    [DataField]
    public TimeSpan EndTime = TimeSpan.Zero;

    [DataField]
    public float ParalyzeChance = 0.35f;

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(3);
}
