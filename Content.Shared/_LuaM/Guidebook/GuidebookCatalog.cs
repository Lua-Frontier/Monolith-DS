/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Guidebook;

[Serializable, NetSerializable]
public sealed class GuidebookCatalogStat
{
    public string Label = string.Empty;
    public string Value = string.Empty;
    public bool Ammunition;
}

[Serializable, NetSerializable]
public sealed class GuidebookEquipmentEntry
{
    public string Id = string.Empty;
    public string PrototypeId = string.Empty;
    public string Kind = string.Empty;
    public List<GuidebookCatalogStat> Stats = new();
    // Canonical resource map paths, so identically named maps do not collide.
    public Dictionary<string, int> Locations = new();
}

[Serializable, NetSerializable]
public sealed class GuidebookVesselEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Group = string.Empty;
    public string MapPath = string.Empty;
    public string Size = string.Empty;
    public List<string> Classes = new();
    public List<string> Engines = new();
    public bool RequireCrew;
    public int ActiveLimit;
    public int Price;
    public bool Purchasable;
    public List<string> Vouchers = new();
    public Dictionary<string, int> Equipment = new();
}

[Serializable, NetSerializable]
public sealed class GuidebookShipyardEntry
{
    public string Id = string.Empty;
    public string PrototypeId = string.Empty;
    public string Name = string.Empty;
    public string ConsoleType = string.Empty;
    public List<string> ConsoleTypes = new();
    public List<string> Vessels = new();
    public Dictionary<string, int> Locations = new();
}

[Serializable, NetSerializable]
public sealed class GuidebookVoucherEntry
{
    public string PrototypeId = string.Empty;
    public string ConsoleType = string.Empty;
    public uint Uses;
    public double CooldownSeconds;
    public bool DestroyOnEmpty;
}

[Serializable, NetSerializable]
public sealed class GuidebookCatalog
{
    public List<string> Ammunition = new();
    public List<GuidebookDroneEntry> Drones = new();
    public List<GuidebookOreTable> OreTables = new();
    public Dictionary<string, List<string>> Hydroponics = new();
    public List<string> Generators = new();
    public Dictionary<string, List<GuidebookCatalogStat>> GeneratorStats = new();
    public List<GuidebookSiteEntry> Sites = new();
    public List<GuidebookAcquisitionEntry> Acquisitions = new();
    public Dictionary<string, List<string>> StoreListings = new();
    public List<string> ResearchDisciplines = new();
    public List<GuidebookVesselEntry> Vessels = new();
    public List<GuidebookShipyardEntry> Shipyards = new();
    public List<GuidebookVoucherEntry> Vouchers = new();
    public List<GuidebookEquipmentEntry> Equipment = new();
    public Dictionary<string, string> MapNames = new();
}

[Serializable, NetSerializable]
public sealed class GuidebookDroneEntry
{
    public string PrototypeId = "";
    public string MapPath = "";
    public string Name = "";
    public List<string> Areas = new();
    public Dictionary<string, int> Loot = new();
}

[Serializable, NetSerializable]
public sealed class GuidebookOreTable
{
    public string Id = "";
    public string Name = "";
    public string Grade = "";
    public string Color = "#aaaaaa";
    public Dictionary<string, float> Probabilities = new();
}

[Serializable, NetSerializable]
public sealed class RequestGuidebookCatalogEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GuidebookCatalogInvalidatedEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class UpdateGuidebookCatalogEvent(GuidebookCatalog data) : EntityEventArgs
{
    public GuidebookCatalog Data = data;
}
