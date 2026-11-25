namespace Content.Client._LuaM.UserInterface.Hud;

public enum HudAccent : byte
{
    Default,
    Command,
    Security,
    Engineering,
    Medical,
    Science,
    Cargo,
    Admin,
    Service,
    Salvage,
    Tsf,
    Syndicate,
    Ussp,
    Pirate,
}

public static class HudAccents
{
    public const string Prefix = "LuaMAcc";

    public static readonly HudAccent[] Tinted =
    {
        HudAccent.Command,
        HudAccent.Security,
        HudAccent.Engineering,
        HudAccent.Medical,
        HudAccent.Science,
        HudAccent.Cargo,
        HudAccent.Admin,
        HudAccent.Service,
        HudAccent.Salvage,
        HudAccent.Tsf,
        HudAccent.Syndicate,
        HudAccent.Ussp,
        HudAccent.Pirate,
    };

    public static readonly Color Science = Color.FromHex("#A98BE8");
    public static readonly Color Cargo = Color.FromHex("#E0893A");
    public static readonly Color Admin = Color.FromHex("#E07AB8");

    public static Color Of(HudAccent accent) => accent switch
    {
        HudAccent.Command => HudPalette.Advisory,
        HudAccent.Security => HudPalette.Bad,
        HudAccent.Engineering => HudPalette.Caution,
        HudAccent.Medical => HudPalette.Good,
        HudAccent.Science => Science,
        HudAccent.Cargo => Cargo,
        HudAccent.Admin => Admin,
        HudAccent.Service => Color.FromHex("#B5D65A"),
        HudAccent.Salvage => Color.FromHex("#B98A5E"),
        HudAccent.Tsf => Color.FromHex("#5B74EA"),
        HudAccent.Syndicate => Color.FromHex("#FF4D6D"),
        HudAccent.Ussp => Color.FromHex("#C83C3C"),
        HudAccent.Pirate => Color.FromHex("#C86BD0"),
        _ => HudPalette.Fg,
    };

    private static readonly (string Key, HudAccent Accent)[] EntityKeys =
    {
        ("syndi", HudAccent.Syndicate),
        ("antag", HudAccent.Syndicate),
        ("nukie", HudAccent.Syndicate),
        ("ussp", HudAccent.Ussp),
        ("tsf", HudAccent.Tsf),
        ("pirate", HudAccent.Pirate),
        ("blackmarket", HudAccent.Pirate),
        ("punk", HudAccent.Pirate),
        ("nfsd", HudAccent.Security),
        ("security", HudAccent.Security),
        ("sheriff", HudAccent.Security),
        ("brig", HudAccent.Security),
        ("criminal", HudAccent.Security),
        ("wanted", HudAccent.Security),
        ("prison", HudAccent.Security),
        ("contraband", HudAccent.Security),
        ("gunnery", HudAccent.Security),
        ("ammo", HudAccent.Security),
        ("salvage", HudAccent.Salvage),
        ("expedition", HudAccent.Salvage),
        ("mining", HudAccent.Salvage),
        ("scrap", HudAccent.Salvage),
        ("medic", HudAccent.Medical),
        ("chem", HudAccent.Medical),
        ("cloning", HudAccent.Medical),
        ("bodyscanner", HudAccent.Medical),
        ("surg", HudAccent.Medical),
        ("viro", HudAccent.Medical),
        ("crewmonitoring", HudAccent.Medical),
        ("engi", HudAccent.Engineering),
        ("atmos", HudAccent.Engineering),
        ("power", HudAccent.Engineering),
        ("solar", HudAccent.Engineering),
        ("sensormonitoring", HudAccent.Engineering),
        ("youtool", HudAccent.Engineering),
        ("science", HudAccent.Science),
        ("research", HudAccent.Science),
        ("robotic", HudAccent.Science),
        ("xeno", HudAccent.Science),
        ("anomaly", HudAccent.Science),
        ("technologydisk", HudAccent.Science),
        ("analysisconsole", HudAccent.Science),
        ("drone", HudAccent.Science),
        ("scidrobe", HudAccent.Science),
        ("booze", HudAccent.Service),
        ("barsign", HudAccent.Service),
        ("boozeo", HudAccent.Service),
        ("chef", HudAccent.Service),
        ("kitchen", HudAccent.Service),
        ("botan", HudAccent.Service),
        ("hydro", HudAccent.Service),
        ("seed", HudAccent.Service),
        ("janitor", HudAccent.Service),
        ("chapel", HudAccent.Service),
        ("cigs", HudAccent.Service),
        ("snack", HudAccent.Service),
        ("coffee", HudAccent.Service),
        ("theater", HudAccent.Service),
        ("clown", HudAccent.Service),
        ("mime", HudAccent.Service),
        ("music", HudAccent.Service),
        ("autotune", HudAccent.Service),
        ("chang", HudAccent.Service),
        ("donut", HudAccent.Service),
        ("soda", HudAccent.Service),
        ("cola", HudAccent.Service),
        ("dinnerware", HudAccent.Service),
        ("comms", HudAccent.Command),
        ("captain", HudAccent.Command),
        ("command", HudAccent.Command),
        ("bridge", HudAccent.Command),
        ("centdrobe", HudAccent.Command),
        ("computerid", HudAccent.Command),
        ("stationrecords", HudAccent.Command),
        ("massmedia", HudAccent.Command),
        ("cargo", HudAccent.Cargo),
        ("market", HudAccent.Cargo),
        ("pallet", HudAccent.Cargo),
        ("shipyard", HudAccent.Cargo),
        ("bank", HudAccent.Cargo),
        ("trade", HudAccent.Cargo),
        ("bounty", HudAccent.Cargo),
        ("stationadmin", HudAccent.Cargo),
    };

    private static readonly Dictionary<string, HudAccent?> EntityCache = new();

    public static HudAccent? ForPrototype(string? id, Robust.Shared.Prototypes.IPrototypeManager protos)
    {
        if (id == null)
            return null;
        if (EntityCache.TryGetValue(id, out var cached))
            return cached;

        HudAccent? result = null;
        var ids = new List<string>();
        Collect(id, protos, ids, 0);
        foreach (var (key, accent) in EntityKeys)
        {
            foreach (var name in ids)
            {
                if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    result = accent;
                    break;
                }
            }

            if (result != null)
                break;
        }

        EntityCache[id] = result;
        return result;
    }

    private static void Collect(string id, Robust.Shared.Prototypes.IPrototypeManager protos, List<string> ids, int depth)
    {
        if (depth > 8 || ids.Contains(id))
            return;

        ids.Add(id);
        if (!protos.TryIndex<Robust.Shared.Prototypes.EntityPrototype>(id, out var proto) || proto.Parents == null)
            return;

        foreach (var parent in proto.Parents)
        {
            if (!parent.EndsWith("Base", StringComparison.Ordinal) && !parent.StartsWith("Base", StringComparison.Ordinal))
                Collect(parent, protos, ids, depth + 1);
        }
    }
    public static string Class(HudAccent accent) => Prefix + accent;

    private static readonly Dictionary<string, HudAccent> Areas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Security"] = HudAccent.Security,
        ["CriminalRecords"] = HudAccent.Security,
        ["FireControl"] = HudAccent.Security,
        ["Weapons"] = HudAccent.Security,
        ["AmmoLoader"] = HudAccent.Security,
        ["AmmoSelector"] = HudAccent.Security,
        ["TurretController"] = HudAccent.Security,
        ["Turrets"] = HudAccent.Security,
        ["Forensics"] = HudAccent.Security,
        ["FederationWanted"] = HudAccent.Security,
        ["Contraband"] = HudAccent.Security,
        ["Brig"] = HudAccent.Security,
        ["SurveillanceCamera"] = HudAccent.Security,

        ["Nuke"] = HudAccent.Syndicate,
        ["NukeOps"] = HudAccent.Syndicate,
        ["WarDeclaration"] = HudAccent.Syndicate,

        ["Pirate"] = HudAccent.Pirate,

        ["Atmos"] = HudAccent.Engineering,
        ["Power"] = HudAccent.Engineering,
        ["Ame"] = HudAccent.Engineering,
        ["ParticleAccelerator"] = HudAccent.Engineering,
        ["Construction"] = HudAccent.Engineering,
        ["RCD"] = HudAccent.Engineering,
        ["Disposal"] = HudAccent.Engineering,
        ["Wires"] = HudAccent.Engineering,
        ["NetworkConfigurator"] = HudAccent.Engineering,
        ["Gravity"] = HudAccent.Engineering,
        ["Singularity"] = HudAccent.Engineering,
        ["Solar"] = HudAccent.Engineering,
        ["SensorMonitoring"] = HudAccent.Engineering,
        ["ShipShields"] = HudAccent.Engineering,
        ["Shields"] = HudAccent.Engineering,
        ["Engineering"] = HudAccent.Engineering,

        ["Medical"] = HudAccent.Medical,
        ["Chemistry"] = HudAccent.Medical,
        ["Cloning"] = HudAccent.Medical,
        ["CloningConsole"] = HudAccent.Medical,
        ["HealthAnalyzer"] = HudAccent.Medical,
        ["Body"] = HudAccent.Medical,
        ["Cyberware"] = HudAccent.Medical,
        ["Surgery"] = HudAccent.Medical,

        ["Research"] = HudAccent.Science,
        ["Xenoarchaeology"] = HudAccent.Science,
        ["Anomaly"] = HudAccent.Science,
        ["SectorAnomaly"] = HudAccent.Science,
        ["Robotics"] = HudAccent.Science,
        ["Silicons"] = HudAccent.Science,
        ["Mech"] = HudAccent.Science,
        ["DroneControl"] = HudAccent.Science,

        ["Cargo"] = HudAccent.Cargo,
        ["Bank"] = HudAccent.Cargo,
        ["Shipyard"] = HudAccent.Cargo,
        ["ShuttleRecords"] = HudAccent.Cargo,
        ["Market"] = HudAccent.Cargo,
        ["Delivery"] = HudAccent.Cargo,

        ["Salvage"] = HudAccent.Salvage,

        ["Communications"] = HudAccent.Command,
        ["CrewManifest"] = HudAccent.Command,
        ["StationRecords"] = HudAccent.Command,
        ["Announcements"] = HudAccent.Command,
        ["Access"] = HudAccent.Command,

        ["Kitchen"] = HudAccent.Service,
        ["Botany"] = HudAccent.Service,
        ["PlantAnalyzer"] = HudAccent.Service,
        ["BarSign"] = HudAccent.Service,

        ["Administration"] = HudAccent.Admin,
        ["Bql"] = HudAccent.Admin,
        ["NPC"] = HudAccent.Admin,
    };

    private static readonly Dictionary<Type, HudAccent> Cache = new();

    public static HudAccent For(Type type)
    {
        if (Cache.TryGetValue(type, out var cached))
            return cached;

        var accent = HudAccent.Default;
        var name = type.FullName ?? string.Empty;
        var parts = name.Split('.');
        for (var i = parts.Length - 2; i >= 2; i--)
        {
            var part = parts[i].TrimStart('_');
            if (Areas.TryGetValue(part, out var found))
            {
                accent = found;
                break;
            }
        }

        Cache[type] = accent;
        return accent;
    }
}
