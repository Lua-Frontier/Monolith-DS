// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._LuaM.Sticky;

/// <summary>
/// Allows sticky items to attach even when their normal blacklist matches this surface.
/// </summary>
[RegisterComponent]
public sealed partial class StickySurfaceOverrideComponent : Component;
