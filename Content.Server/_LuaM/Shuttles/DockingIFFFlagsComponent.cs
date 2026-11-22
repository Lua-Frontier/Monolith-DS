using Content.Shared.Shuttles.Components;

namespace Content.Server._LuaM.Shuttles;

/// <summary>
/// IFF flags a station put on this ship while it was docked, the only ones taken back on undocking.
/// Flags the ship had on its own, like the imperial hiding of Vanguard ships, stay.
/// </summary>
[RegisterComponent]
public sealed partial class DockingIFFFlagsComponent : Component
{
    [ViewVariables]
    public IFFFlags Flags;
}
