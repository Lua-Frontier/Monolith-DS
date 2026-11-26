// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Grab;

/// <summary>
/// Raised on the puller when they try to pull the entity they are already pulling.
/// Set <see cref="Handled"/> to keep the pull instead of releasing it.
/// </summary>
[ByRefEvent]
public record struct RegrabAttemptEvent(EntityUid Target, bool Handled = false);
