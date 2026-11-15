using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Buckle;

// -- Точки пристёгивания в локальных координатах стрепа; каждому пристёгнутому достаётся свободная
[RegisterComponent, NetworkedComponent]
public sealed partial class StrapSlotsComponent : Component
{
    [DataField(required: true)]
    public List<Vector2> Offsets = new();

    [ViewVariables]
    public Dictionary<EntityUid, int> Requested = new();
}
