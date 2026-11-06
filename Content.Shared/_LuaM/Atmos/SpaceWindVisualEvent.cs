using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Atmos;

[Serializable, NetSerializable]
public sealed class SpaceWindVisualEvent(NetCoordinates coordinates, Angle rotation) : EntityEventArgs
{
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly Angle Rotation = rotation;
}
