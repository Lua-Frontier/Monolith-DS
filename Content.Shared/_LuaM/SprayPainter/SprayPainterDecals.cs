using Content.Shared.Decals;

namespace Content.Shared._LuaM.SprayPainter;

public static class SprayPainterDecals
{
    /// <summary>
    /// Station markings can be painted and removed, dirt and other decals cannot.
    /// </summary>
    public static bool IsPaintable(DecalPrototype proto)
    {
        return (proto.Tags.Contains("station") || proto.Tags.Contains("markings"))
            && !proto.Tags.Contains("dirty");
    }
}
