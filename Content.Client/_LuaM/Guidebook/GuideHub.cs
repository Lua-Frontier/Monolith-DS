/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Client.Guidebook.RichText;
using Content.Client.Guidebook.Controls;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Illustrated navigation using the existing guide tree, without alternate page addresses.</summary>
public sealed class GuideHub : BoxContainer, IDocumentTag
{
    private static readonly Dictionary<string, string> LobbyJobs = new()
    {
        ["ColossusMercenaries"] = "Contractor",
        ["ColossusMMCRoles"] = "MMCEmployee",
        ["ColossusTSFRoles"] = "Bailiff",
        ["ColossusNordfallRoles"] = "PDVTarkhan",
        ["ColossusUSSPRoles"] = "USSPCommissar",
        ["Viper_Group"] = "VGCommander",
        ["Caelestinus"] = "StationRepresentative",
        ["ColossusMedicalFaction"] = "DirectorOfCare",
        ["ColossusSpecialRoles"] = "Borg",
    };
    // A hub uses pictograms only when every entry has an authored one; otherwise all entries are numbered.
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["ColossusActivities"] = "work",
        ["ColossusMining"] = "mining",
        ["ColossusExpeditionsHub"] = "expedition",
        ["ColossusExtraction"] = "mining",
        ["ColossusBotany"] = "botany",
        ["ColossusRndHub"] = "research",
        ["IndustryResearch"] = "research",
        ["ColossusProduction"] = "production",
        ["Engineering"] = "engineering",
        ["ColossusCombat"] = "combat",
        ["Medical"] = "medical",
        ["SectorTopology"] = "map",
        ["LuaMStations"] = "station",
        ["LuaMWorldObjects"] = "object",
        ["LuaMGameModes"] = "event",
        ["LuaMAsteroids"] = "asteroid",
        ["ColossusNebulae"] = "nebula",
        ["BeginnerGuide"] = "newcomer",
        ["StartingGear"] = "clothes",
        ["ColossusMoney"] = "coins",
        ["Hiring"] = "crew",
        ["Jobs"] = "work",
        ["Species"] = "species",
        ["Writing"] = "writing",
        ["Shipyard"] = "shiptrade",
        ["ColossusCatalogueShipyards"] = "fleet",
        ["Piloting"] = "helm",
        ["ColossusOrientation"] = "compass",
        ["ColossusShipArmament"] = "cannon",
        ["ColossusShipRepair"] = "repair",
        ["GridClaimer"] = "flag",
        ["ColossusReferenceEquipment"] = "reference",
        ["ColossusReferenceResources"] = "ingots",
        ["Chemicals"] = "chemistry",
        ["ColossusRecipes"] = "recipes",
        ["ColossusMercenaries"] = "work",
        ["ColossusMMCRoles"] = "production",
        ["ColossusTSFRoles"] = "shield",
        ["ColossusNordfallRoles"] = "combat",
        ["ColossusUSSPRoles"] = "flag",
        ["Viper_Group"] = "cannon",
        ["Caelestinus"] = "station",
        ["ColossusMedicalFaction"] = "medical",
        ["ColossusSpecialRoles"] = "species",
        ["ColossusMMCFaction"] = "production",
        ["MMC"] = "production",
        ["Economy"] = "coins",
        ["ArcFurnace"] = "ingots",
        ["IndustryAutomation"] = "engineering",
        ["LuaMLoreMMC"] = "production",
        ["PDV_Entry"] = "combat",
        ["ColossusNordfallCharter"] = "manual",
        ["TSFMC_Entry"] = "shield",
        ["TSFMC_org"] = "crew",
        ["TSFMC_SOP"] = "work",
        ["ColossusTSFCode"] = "manual",
        ["USSP_Entry"] = "flag",
        ["USSP_SoP"] = "manual",
        ["ColossusCatalogueWeapons"] = "combat",
        ["ColossusCatalogueAmmo"] = "cannon",
        ["ColossusCatalogueArmor"] = "clothes",
        ["ColossusCatalogueUplinks"] = "reference",
        ["ColossusCataloguePrices"] = "coins",
        ["ColossusPrecursors"] = "station",
        ["CorticalBorerPracticalGuide"] = "species",
        ["ChimeraPracticalGuide"] = "asteroid",
        ["Xenoborgs"] = "shield",
        ["ColossusEventSyndicateCache"] = "object",
        ["ColossusEventTSFVault"] = "shield",
        ["ColossusEventSyndicateShuttle"] = "ship",
        ["ColossusEventPreFracture"] = "compass",
        ["ColossusEventMonolithFragments"] = "asteroid",
        ["LuaMMonolithWreck"] = "station",
        ["ColossusEventPlanetoids"] = "expedition",
        ["ColossusEventSalvage"] = "repair",
        ["ColossusEventADSFleet"] = "fleet",
        ["ColossusEventAsakimShips"] = "combat",
        ["ColossusEventDeliveries"] = "work",
        ["Timeline"] = "event",
        ["LuaMLoreSector"] = "map",
        ["MonoStarSystem"] = "sector",
        ["LuaMLoreMonolith"] = "object",
        ["LuaMLoreColoss"] = "station",
        ["LuaMLoreTSF"] = "shield",
        ["LuaMLoreAN"] = "combat",
        ["LuaMLoreUSSP"] = "flag",
        ["LuaMLoreViper"] = "cannon",
        ["LuaMColossusCentral"] = "station",
        ["LuaMMedical"] = "medical",
        ["LuaMCamelot"] = "flag",
        ["LuaMHalicon"] = "fleet",
        ["LuaMHelios"] = "shield",
        ["LuaMTradeMall"] = "coins",
        ["ColossusSiteTradeDepots"] = "object",
        ["ColossusSiteBlackMarket"] = "work",
        ["ColossusSiteSevastopol"] = "reference",
        ["ColossusSiteMining"] = "mining",
        ["ColossusSiteHokkaido"] = "production",
        ["LuaMArcturus"] = "ship",
        ["ColossusSiteEdison"] = "event",
        ["ColossusSiteZvezdaLuaM"] = "station",
        ["ColossusSiteZetaNode"] = "compass",
        ["ColossusSiteBeacons"] = "flag",
        ["ColossusSiteLPBravo"] = "sector",
        ["ColossusSiteAnomalousLab"] = "research",
        ["ColossusSiteTinnia"] = "recipes",
        ["ColossusSiteCaseysCasino"] = "coins",
        ["ColossusSiteOmnichurch"] = "lore",
        ["ColossusSiteThePit"] = "combat",
        ["ColossusSiteGrifty"] = "object",
        ["ColossusSiteMcHobo"] = "repair",
        ["ColossusSiteJupiter"] = "station",
        ["ColossusSiteLancelot"] = "shield",
        ["ColossusSitePolaris"] = "expedition",
        ["ColossusSiteAutomatedTanker"] = "chemistry",
        ["ColossusSiteZenith"] = "cannon",
    };
    private static readonly Dictionary<string, string> Entities = new()
    {
        ["ColossusMining"] = "Pickaxe",
        ["IndustryDrilling"] = "MachineShipDrill",
        ["ColossusLaserDrill"] = "LaserDrill",
        ["GasMining"] = "GasMiningDrill",
        ["IndustryResearch"] = "ComputerResearchAndDevelopment",
        ["ColossusCatalogueResearch"] = "ResearchAndDevelopmentServer",
        ["ColossusArtifacts"] = "MachineArtifactAnalyzer",
        ["ColossusAnomalies"] = "AnomalyScanner",
        ["MechsBase"] = "MechRipley",
        ["S1Mechs"] = "MechGygax",
        ["S2Mechs"] = "AFInterceptor",
        ["S4Mechs"] = "AFFlail",
        ["IndustryFabricators"] = "Autolathe",
        ["ColossusMechsHub"] = "MechRipley2",
        ["Construction"] = "TableReinforced",
        ["ExpandingRepairingStation"] = "FloorTileItemSteel",
        ["WirePanels"] = "Wirecutter",
        ["Airlocks"] = "AirlockGlass",
        ["ColossusPower"] = "SubstationBasic",
        ["ColossusCatalogueEngineering"] = "PortableGeneratorPacman",
        ["ColossusTEG"] = "TegCenter",
        ["ColossusEdison"] = "SolarPanel",
        ["Ramping"] = "SMESBasic",
        ["ShipyardNFROperation"] = "NuclearReactorNormal",
        ["Pumps"] = "GasPressurePump",
        ["MixingAndFiltering"] = "GasMixer",
        ["Thermomachines"] = "GasThermoMachineFreezer",
        ["Pipes"] = "GasPipeTJunction",
        ["PressureRegulator"] = "GasPressureRegulator",
        ["DeviceMonitoringAndControl"] = "AirSensor",
        ["AirAlarms"] = "AirAlarm",
        ["AtmosphericAlertsComputer"] = "ComputerAlert",
        ["AtmosphericNetworkMonitor"] = "ComputerAtmosMonitoring",
        ["ColossusAtmos"] = "OxygenCanister",
        ["ColossusAtmosEquipment"] = "GasVolumePump",
        ["ColossusGasSynthesis"] = "GasMixer",
        ["ColossusAtmosMonitoring"] = "ComputerAtmosMonitoring",
        ["ColossusHumanity"] = "HandheldHealthAnalyzer",
        ["ColossusHeadImplants"] = "BasicCyberneticEyes",
        ["ColossusCyberneticOrgans"] = "BasicCyberneticHeart",
        ["ColossusMantisBlades"] = "OrganArmMantisBlade",
        ["ColossusServiceImplants"] = "MedicalTrackingImplanter",
        ["WeaponsSystems"] = "ComputerGunneryConsole",
        ["ColossusCatalogueShipWeapons"] = "WeaponTurretCharon",
        ["ColossusFireControlServers"] = "GunneryServerLow",
        ["Bank"] = "ComputerBankATM",
        ["SafetyDepositBox"] = "SafetyDepositBoxMedium",
        ["CargoHauling"] = "CrateTradeSecureNormal",
        ["ColossusCourier"] = "DeliveryCourierPDA",
        ["LuaMContraband"] = "ContrabandPallet",
        ["LuaMDroneLoot"] = "ClothingBackpackDroneLootT1",
        ["ColossusCommodities"] = "OxygenCanister",
        ["Expeditions"] = "ComputerSalvageExpedition",
        ["ColossusExpeditionEnemies"] = "MobCarp",
        ["MobsExpeditionGuideAberrantFlesh"] = "MobFleshJaredExpeditions",
        ["MobsExpeditionGuideArgocytes"] = "MobArgocyteSkitterExpeditions",
        ["MobsExpeditionGuideBloodCult"] = "MobBloodCultistAcolyte",
        ["MobsExpeditionGuideCarps"] = "MobCarp",
        ["MobsExpeditionGuideDinosaurs"] = "MobDinosaurRaptorExpeditions",
        ["MobsExpeditionGuideExplorers"] = "MobExplorerMeleeT1",
        ["MobsExpeditionGuideMercenaries"] = "MobMercenarySoldierPistol",
        ["MobsExpeditionGuideGangers"] = "MobPunkGangerMeleeT1",
        ["MobsExpeditionGuideSilicons"] = "MobRogueSiliconScrap",
        ["MobsExpeditionGuideSyndicate"] = "MobSyndicateNavalDeckhandA",
        ["MobsExpeditionGuideXenos"] = "MobXenoDrone",
        ["ColossusGasTritium"] = "TritiumCanister",
        ["ColossusGasFrezon"] = "FrezonCanister",
        ["ColossusGasBZ"] = "BZCanister",
        ["ColossusGasHealium"] = "HealiumCanister",
        ["ColossusGasNitrium"] = "NitriumCanister",
        ["ColossusGasPluoxium"] = "PluoxiumCanister",
        ["MedicalDoctor"] = "HandheldHealthAnalyzer",
        ["ColossusSurgery"] = "Scalpel",
        ["Cryogenics"] = "CryoPod",
        ["Cloning"] = "CloningPod",
        ["Letoferol"] = "SyringeLetoferol",
        ["MedicalBounties"] = "CrateMedical",
        ["ColossusCybernetics"] = "BasicCyberneticHeart",
        ["ColossusPrecursors"] = "PlayerBorgRedacted",
        ["CorticalBorerPracticalGuide"] = "MobCorticalBorer",
        ["ChimeraPracticalGuide"] = "MobLetoferolHorror",
        ["Xenoborgs"] = "BorgChassisSelectableXenoborg",
        ["Drinks"] = "DrinkWaterBottleFull",
        ["FoodRecipes"] = "FoodBreadPlain",
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
        var entries = guide.Children.Where(id => selected == null || selected.Contains(id)).ToArray();
        var lobbyIcons = root == "Factions";
        if (lobbyIcons && !entries.All(id => LobbyJobs.ContainsKey(id))) return false;
        var illustrated = entries.All(id => Icons.ContainsKey(id));
        var portraits = root == "Species" && entries.All(id => prototypes.HasIndex<SpeciesPrototype>(id));
        var sprites = entries.All(id => Entities.TryGetValue(id, out var entity) && prototypes.HasIndex<EntityPrototype>(entity));
        foreach (var id in entries)
        {
            if (selected != null && !selected.Contains(id)) continue;
            index++;
            var target = prototypes.Index(id);
            var button = new Button { HorizontalExpand = true, MinHeight = 64, ToolTip = Loc.GetString(target.Name) };
            var body = new BoxContainer { HorizontalExpand = true, SeparationOverride = 10, Margin = new Thickness(8) };
            if (lobbyIcons)
                body.AddChild(new GuideLobbyJobIcon(LobbyJobs[id]));
            else if (portraits || sprites)
            {
                string entity = portraits ? prototypes.Index<SpeciesPrototype>(id).DollPrototype : Entities[id];
                var portrait = new GuideEntityEmbed(entity, false, false, true) { PreviewPowered = true };
                portrait.SetSize = new(48, 48);
                var view = portrait.FindControl<SpriteView>("View");
                view.SetSize = new(48, 48);
                view.Scale = System.Numerics.Vector2.One;
                view.Stretch = SpriteView.StretchMode.Fill;
                body.AddChild(portrait);
            }
            else if (illustrated)
                body.AddChild(new GuideNavigationIcon(Icons[id]));
            else body.AddChild(new Label { Text = index.ToString("00"), SetWidth = 32,
                Align = Label.AlignMode.Center, ModulateSelfOverride = Color.FromHex("#73BFD3"), VerticalAlignment = VAlignment.Top });
            var title = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Top };
            title.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString(target.Name)));
            var labels = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
            labels.AddChild(title);
            body.AddChild(labels); button.AddChild(body);
            button.OnPressed += _ => { if (button.TryGetParentHandler<ILinkClickHandler>(out var handler)) handler.HandleClick(id); };
            cards.AddChild(button);
        }
        AddChild(cards); control = this; return true;
    }
}
