// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Grab;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.GrabIntent;

/// <summary>
/// The entity can be grabbed when pulled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrabbableComponent : Component
{
    [DataField, AutoNetworkedField]
    public GrabStage GrabStage = GrabStage.No;

    /// <summary>
    /// Multiplies the escape chance, e.g. a Corporate Judo armbar.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EscapeAttemptModifier = 1f;

    [DataField, AutoNetworkedField]
    public float GrabEscapeChance = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextEscapeAttempt = TimeSpan.Zero;

    [DataField]
    public TimeSpan EscapeAttemptCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Is flying towards a table after being slammed onto it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BeingTabled;

    [DataField]
    public float TabledThrowSpeed = 5f;

    [DataField]
    public float TabledStaminaDamage = 40f;

    [DataField]
    public float TabledDamage = 10f;

    [DataField]
    public TimeSpan PostTabledDuration = TimeSpan.FromSeconds(3);
}
