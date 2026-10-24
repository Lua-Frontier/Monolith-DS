// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.DeadSpace.Necromorphs.Unitology.Components;

/// <summary>
/// Starts a one-shot timer and enslaves half of the eligible online players when it expires.
/// </summary>
[RegisterComponent]
public sealed partial class UnitologyObeliskComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromHours(1);

    [ViewVariables]
    public TimeSpan TriggerAt;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Triggered;
}
