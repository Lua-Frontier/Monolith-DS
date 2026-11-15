/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using Robust.Shared.Serialization;
namespace Content.Shared._LuaM.Guidebook;
[Serializable, NetSerializable]
public sealed class GuidebookAcquisitionOffer
{
    public string SourceId = "";
    public string SourceName = "";
    public string Method = "";
    public string Detail = "";
    public string CategoryId = "";
    public string CategoryName = "";
    public string Faction = "Наёмники";
}
[Serializable, NetSerializable]
public sealed class GuidebookAcquisitionEntry
{
    public string PrototypeId = "";
    public string Kind = "";
    public string IdentityKey = "";
    public List<GuidebookAcquisitionOffer> Offers = new();
    public List<GuidebookCatalogStat> Stats = new();
    public List<GuidebookCatalogStat> ShieldStats = new();
}
