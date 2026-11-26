// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.GrabIntent;

/// <summary>
/// Marks a virtual item taken by a grab stage, the pull keeps its own one.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GrabVirtualItemComponent : Component;
