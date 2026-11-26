// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Common.MartialArts;

/// <summary>
/// The entity can't breathe until <see cref="BlockedTime"/>, after a Krav Maga lung punch.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KravMagaBlockedBreathingComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan BlockedTime = TimeSpan.Zero;
}
