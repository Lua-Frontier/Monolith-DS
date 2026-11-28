namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Marks an ammunition box or a box magazine.
/// Lets prototypes match all of them at once, for example in a storage blacklist.
/// </summary>
[RegisterComponent]
public sealed partial class AmmoBoxComponent : Component;
