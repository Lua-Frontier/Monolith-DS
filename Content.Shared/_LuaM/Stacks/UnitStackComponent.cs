using Robust.Shared.Map;

namespace Content.Shared._LuaM.Stacks;

/// <summary>
/// A stack of things that are used one at a time (produce, glass shards): eaten, sliced, welded, stepped on.
/// A hand only ever holds one unit of it, and whatever is done to the stack is done to one unit, while the rest
/// stays where it was.
/// </summary>
[RegisterComponent]
public sealed partial class UnitStackComponent : Component
{
    /// <summary>
    /// Where the stack lay on the floor right before it was picked up.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? FloorOrigin;

    /// <summary>
    /// The container the stack was taken out of right before it was picked up.
    /// </summary>
    [ViewVariables]
    public (EntityUid Owner, string Id)? ContainerOrigin;
}
