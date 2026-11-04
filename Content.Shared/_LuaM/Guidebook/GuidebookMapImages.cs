/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using Robust.Shared.Serialization;
namespace Content.Shared._LuaM.Guidebook;

/// <summary>Temporarily disabled: guidebook rendering must not start with builds or game sessions.</summary>
public static class GuidebookMapRendering
{
    public static bool Enabled => false;
}

[Serializable, NetSerializable]
public sealed class GuidebookImagesInvalidatedEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class RequestGuidebookMapImage(string path, int request, string knownVersion, int part = 0) : EntityEventArgs
{
    public string Path = path;
    public int Request = request;
    public string KnownVersion = knownVersion;
    public int Part = part;
}

[Serializable, NetSerializable]
public sealed class GuidebookMapImageChunk(int request, string version, int offset, int total, byte[] data, string status = "") : EntityEventArgs
{
    public int Request = request;
    public string Version = version;
    public int Offset = offset;
    public int Total = total;
    public byte[] Data = data;
    public string Status = status;
    public int Parts = 1;
}
