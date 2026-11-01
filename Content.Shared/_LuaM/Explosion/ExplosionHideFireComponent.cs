using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Explosion;

/// <summary>
/// Put on the explosion visuals entity when the explosion uses the RMC visuals.
/// The client then skips the atmos fire tiles of this explosion, the light stays.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ExplosionHideFireComponent : Component;
