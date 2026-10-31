using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Shuttles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), UnsavedComponent]
public sealed partial class RadarGridEdgesComponent : Component
{
    // -- пары точек в координатах грида: начало и конец каждой метки
    [ViewVariables, AutoNetworkedField]
    public List<Vector2> Edges = new();

    [ViewVariables]
    public HashSet<EntityUid> Markers = new();
}
