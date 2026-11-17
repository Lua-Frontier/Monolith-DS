using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent] // LuaM
[Access(typeof(SharedGunSystem), typeof(Content.Shared._LuaM.Performance.DeletedRefStateSystem))] // LuaM
public sealed partial class TargetedProjectileComponent : Component
{
    [DataField] // LuaM
    public EntityUid Target;
}
