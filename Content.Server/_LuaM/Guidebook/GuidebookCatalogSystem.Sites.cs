/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
namespace Content.Server._LuaM.Guidebook;
public sealed partial class GuidebookCatalogSystem
{
    private static readonly HashSet<string> SiteFacilities = new()
    { "ShipyardConsole", "Store", "Lathe", "ResearchConsole", "CloningConsole", "BankATM", "CargoBountyConsole", "CargoOrderConsole", "MaterialReclaimer", "ReagentDispenser", "SalvageExpeditionConsole", "MedicalScanner", "VendingMachine" };
    private GuidebookSiteEntry ReadSite(string name, string path, bool hidden, MappingDataNode data)
    {
        var site = new GuidebookSiteEntry { Name = name, MapPath = path, Hidden = hidden };
        foreach (var group in data.Get<SequenceDataNode>("entities").OfType<MappingDataNode>())
        {
            var id = group.Get<ValueDataNode>("proto").Value;
            if (!_prototype.TryIndex<EntityPrototype>(id, out var proto)) continue;
            var count = group.Get<SequenceDataNode>("entities").Count;
            if (proto.Components.Keys.Any(SiteFacilities.Contains)) site.Facilities[id] = site.Facilities.GetValueOrDefault(id) + count;
            if ((proto.Components.ContainsKey("HTN") && proto.Components.ContainsKey("MobState")) ||
                proto.Components.ContainsKey("RandomSpawner") && (proto.Name.Contains("дрон", StringComparison.CurrentCultureIgnoreCase)
                    || proto.Name.Contains("боец", StringComparison.CurrentCultureIgnoreCase)))
                site.Inhabitants[id] = site.Inhabitants.GetValueOrDefault(id) + count;
            if (proto.Components.ContainsKey("Contraband") || proto.Components.ContainsKey("XenoArtifact")
                || id.StartsWith("MonolithicCrystal", StringComparison.Ordinal) || id.Contains("BlackBox", StringComparison.Ordinal))
                site.Finds[id] = site.Finds.GetValueOrDefault(id) + count;
        }
        return site;
    }
}
