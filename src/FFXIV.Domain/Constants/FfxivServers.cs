namespace FFXIV.Domain.Constants;

public static class FfxivServers
{
    public static readonly Dictionary<string, string[]> DataCenters = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        ["Aether"] = ["Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren"],
        ["Primal"] = ["Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros"],
        ["Crystal"] = ["Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera"],
        ["Dynamis"] = ["Halicarnassus", "Maduin", "Marilith", "Seraph", "Cuchulainn", "Kraken", "Rafflesia", "Golem"],

        // Europe
        ["Chaos"] = ["Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan"],
        ["Light"] = ["Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark"],

        // Japan
        ["Elemental"] = ["Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Tonberry", "Typhon"],
        ["Gaia"] = ["Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima"],
        ["Mana"] = ["Anima", "Asura", "Chocobo", "Hades", "Ixion", "Masamune", "Pandaemonium", "Titan"],
        ["Meteor"] = ["Belias", "Mandragora", "Ramuh", "Shinryu", "Unicorn", "Valefor", "Yojimbo", "Zeromus"],

        // Oceania
        ["Materia"] = ["Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan"],
    };

    private static readonly Dictionary<string, string> ServerToDataCenter = BuildServerLookup();

    private static Dictionary<string, string> BuildServerLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dc, servers) in DataCenters)
        {
            foreach (var server in servers)
                lookup[server] = dc;
        }
        return lookup;
    }

    private static readonly Dictionary<string, string> DataCenterToRegion = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aether"] = "North America",
        ["Primal"] = "North America",
        ["Crystal"] = "North America",
        ["Dynamis"] = "North America",
        ["Chaos"] = "Europe",
        ["Light"] = "Europe",
        ["Elemental"] = "Japan",
        ["Gaia"] = "Japan",
        ["Mana"] = "Japan",
        ["Meteor"] = "Japan",
        ["Materia"] = "Oceania",
    };

    public static IEnumerable<string> AllWorlds => ServerToDataCenter.Keys;

    public static bool IsValidServer(string server) =>
        ServerToDataCenter.ContainsKey(server);

    public static bool IsValidWorldOrDc(string worldOrDc) =>
        ServerToDataCenter.ContainsKey(worldOrDc) || DataCenters.ContainsKey(worldOrDc);

    public static string? GetDataCenter(string server) =>
        ServerToDataCenter.GetValueOrDefault(server);

    public static string? GetRegionForDataCenter(string dataCenter) =>
        DataCenterToRegion.GetValueOrDefault(dataCenter);

    public static List<WorldGroup> GetWorldsGrouped()
    {
        var groups = new Dictionary<string, WorldGroup>();

        foreach (var (dc, servers) in DataCenters)
        {
            var region = DataCenterToRegion.GetValueOrDefault(dc, "Unknown");
            if (!groups.TryGetValue(region, out var group))
            {
                group = new WorldGroup { Region = region };
                groups[region] = group;
            }
            group.DataCenters.Add(new DataCenterInfo
            {
                Name = dc,
                Worlds = [.. servers]
            });
        }

        return [.. groups.Values];
    }
}

public class WorldGroup
{
    public string Region { get; set; } = string.Empty;
    public List<DataCenterInfo> DataCenters { get; set; } = [];
}

public class DataCenterInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> Worlds { get; set; } = [];
}
