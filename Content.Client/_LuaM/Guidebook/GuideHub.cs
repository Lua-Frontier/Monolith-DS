/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Client.Guidebook.Richtext;
using Content.Client.Guidebook.RichText;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.ContentPack;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Illustrated navigation using the existing guide tree, without alternate page addresses.</summary>
public sealed class GuideHub : BoxContainer, IDocumentTag
{
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["Medical"] = "Medkit", ["ColossusSurgery"] = "Scalpel", ["ColossusCybernetics"] = "BasicCyberneticHeart",
        ["ColossusRndHub"] = "ComputerResearchAndDevelopment", ["IndustryResearch"] = "ComputerResearchAndDevelopment",
        ["ColossusArtifacts"] = "MachineArtifactAnalyzer", ["ColossusAnomalies"] = "AnomalyScanner",
        ["ColossusBotany"] = "HydroponicsTrayEmpty", ["ColossusMining"] = "MiningDrill",
        ["ColossusLaserDrill"] = "LaserDrill", ["IndustryDrilling"] = "MachineShipDrill", ["GasMining"] = "GasMiningDrill",
        ["CargoHauling"] = "CrateTradeSecureNormal", ["ColossusCourier"] = "DeliveryCourierPDA",
        ["LuaMDroneLoot"] = "ClothingBackpackDroneLootT1", ["LuaMContraband"] = "CrateContrabandStorage",
        ["IndustryMachineParts"] = "RPED", ["ColossusMechsHub"] = "RipleyChassis", ["IndustryFabricators"] = "Autolathe",
        ["ColossusPower"] = "APCBasic", ["ColossusTEG"] = "TegCenter", ["ShipyardNFROperation"] = "NuclearReactorNormal",
        ["ColossusShipRepair"] = "ShipRepairDevice", ["Piloting"] = "ComputerShuttle", ["ColossusOrientation"] = "ComputerShuttle",
        ["ColossusCatalogueShipyards"] = "ComputerShipyard", ["WeaponsSystems"] = "ComputerGunneryConsole",
        ["ColossusCatalogueShipWeapons"] = "WeaponTurretCharon", ["ColossusCatalogueAmmo"] = "MagazinePistol",
        ["ColossusCatalogueArmor"] = "ClothingOuterHardsuitSpatio", ["ColossusCatalogueUplinks"] = "VendingMachineTSFMCUplink",
        ["ColossusTSFRoles"] = "BookTSFSOP", ["ColossusNordfallRoles"] = "BookNordfallSOP",
        ["ColossusMMCRoles"] = "IndustrialFurnaceEconomy", ["ColossusMedicalFaction"] = "Medkit",
        ["Bank"] = "ComputerBankATM", ["SafetyDepositBox"] = "SafetyDepositBox",
    };
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        if (!args.TryGetValue("Root", out var root) || !prototypes.TryIndex<GuideEntryPrototype>(root, out var guide)) return false;
        HorizontalExpand = true;
        var cards = new GuideEntityCards { ItemWidth = 246, Frame = false, Margin = new Thickness(0, 0, 0, 8) };
        var index = 0;
        var selected = args.GetValueOrDefault("Entries")?.Split(',').Select(s => s.Trim()).ToHashSet();
        foreach (var id in guide.Children)
        {
            if (selected != null && !selected.Contains(id)) continue;
            index++;
            var target = prototypes.Index(id);
            using var reader = IoCManager.Resolve<IResourceManager>().ContentFileReadText(target.Text);
            var source = reader.ReadToEnd();
            Icons.TryGetValue(id, out var entity);
            if (prototypes.TryIndex<SpeciesPrototype>(id, out var species)) entity = species.DollPrototype;
            entity ??= Regex.Match(source, "Entity=\"([^\"]+)\"").Groups[1].Value;
            var button = new Button { HorizontalExpand = true, MinHeight = 72, ToolTip = Loc.GetString(target.Name) };
            var body = new BoxContainer { HorizontalExpand = true, SeparationOverride = 10, Margin = new Thickness(8) };
            if (!string.IsNullOrEmpty(entity) && prototypes.TryIndex<EntityPrototype>(entity, out var p))
            {
                var texture = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().GetPrototypeIcon(p).Default;
                body.AddChild(new TextureRect { Texture = texture, SetSize = new(40, 40), CanShrink = true,
                    Stretch = TextureRect.StretchMode.KeepAspectCentered, VerticalAlignment = VAlignment.Center });
            }
            else body.AddChild(new Label { Text = index.ToString("00"), ModulateSelfOverride = Color.FromHex("#73BFD3"), VerticalAlignment = VAlignment.Center });
            var title = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            title.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString(target.Name)));
            var labels = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };
            labels.AddChild(title);
            var intro = Regex.Match(source, "(?m)^# [^\\n]+\\n\\s*\\n([^<#\\n][^\\n]+)").Groups[1].Value;
            intro = Regex.Replace(intro, "\\[textlink=\"([^\"]*)\"[^\\]]*\\]", "$1");
            intro = Regex.Replace(intro, "\\[[^\\]]*\\]", "");
            if (intro.Length > 150) intro = Regex.Match(intro, "^.{15,150}?[.!?](?: |$)").Value.Trim();
            if (intro.Length > 0 && !intro.Contains('·'))
            {
                var description = new RichTextLabel { HorizontalExpand = true, ModulateSelfOverride = Color.FromHex("#B4C4CD") };
                description.SetMessage(FormattedMessage.FromUnformatted(intro)); labels.AddChild(description);
            }
            body.AddChild(labels); button.AddChild(body);
            button.OnPressed += _ => { if (button.TryGetParentHandler<ILinkClickHandler>(out var handler)) handler.HandleClick(id); };
            cards.AddChild(button);
        }
        AddChild(cards); control = this; return true;
    }
}
