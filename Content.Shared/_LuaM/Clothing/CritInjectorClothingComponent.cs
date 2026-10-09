using Content.Shared.Chemistry.Components;
using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Clothing;

/// <summary>
/// Worn clothing that injects a solution into its wearer when they become critical.
/// </summary>
[RegisterComponent]
public sealed partial class CritInjectorClothingComponent : Component
{
    /// <summary>
    /// What gets injected into the bloodstream.
    /// </summary>
    [DataField(required: true)]
    public Solution Solution = new();

    /// <summary>
    /// Minimum time between injections.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>
    /// When the clothing can inject again.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextInject;
}
