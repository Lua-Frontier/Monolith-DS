using Content.Shared.Decals;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.SprayPainter;

/// <summary>
/// What a spray painter does when it is used on the floor.
/// </summary>
[Serializable, NetSerializable]
public enum DecalPaintMode : byte
{
    Off = 0,
    Add = 1,
    Remove = 2,
}

[Serializable, NetSerializable]
public sealed class SprayPainterSetDecalMessage(ProtoId<DecalPrototype> protoId) : BoundUserInterfaceMessage
{
    public ProtoId<DecalPrototype> DecalPrototype = protoId;
}

[Serializable, NetSerializable]
public sealed class SprayPainterSetDecalColorMessage(Color? color) : BoundUserInterfaceMessage
{
    public Color? Color = color;
}

[Serializable, NetSerializable]
public sealed class SprayPainterSetDecalSnapMessage(bool snap) : BoundUserInterfaceMessage
{
    public bool Snap = snap;
}

[Serializable, NetSerializable]
public sealed class SprayPainterSetDecalAngleMessage(int angle) : BoundUserInterfaceMessage
{
    public int Angle = angle;
}
