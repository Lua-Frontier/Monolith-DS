/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using Robust.Shared.Serialization;
namespace Content.Shared._LuaM.Guidebook;
[Serializable, NetSerializable]
public sealed class GuidebookSiteEntry
{
    public string Name = "";
    public string MapPath = "";
    public bool Hidden;
    public Dictionary<string, int> Facilities = new();
    public Dictionary<string, int> Inhabitants = new();
    public Dictionary<string, int> Finds = new();
}
