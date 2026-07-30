// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Shared._LuaM.Sticky;

/// <summary>
/// Changes an item's sprite scale while it is stuck to a marked surface.
/// </summary>
[RegisterComponent]
public sealed partial class ScaleWhenStuckComponent : Component
{
    [DataField]
    public Vector2 Scale = new(0.7f, 0.7f);

    public Vector2? OriginalScale;
}
