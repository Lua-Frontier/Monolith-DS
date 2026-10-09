using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Implants;

/// <summary>
/// An implant that injects a solution into its owner from a hotbar action.
/// Its action charges recharge one by one, and it can inject itself when the owner goes critical.
/// </summary>
[RegisterComponent]
public sealed partial class InjectorImplantComponent : Component
{
    /// <summary>
    /// What gets injected into the bloodstream on every use.
    /// </summary>
    [DataField(required: true)]
    public Solution Solution = new();

    /// <summary>
    /// How long it takes to restore one charge.
    /// </summary>
    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Whether the implant injects automatically when its owner becomes critical.
    /// </summary>
    [DataField]
    public bool InjectOnCritical = true;

    /// <summary>
    /// Minimum time between automatic injections, so repeated crits do not drain every charge.
    /// </summary>
    [DataField]
    public TimeSpan AutoInjectCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// When the implant can inject automatically again.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextAutoInject;

    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>
    /// When the next charge is restored. Null while the charges are full.
    /// </summary>
    [ViewVariables]
    public TimeSpan? NextRecharge;
}

public sealed partial class UseInjectorImplantEvent : InstantActionEvent;
