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

    public static bool IsValidServer(string server) =>
        ServerToDataCenter.ContainsKey(server);

    public static string? GetDataCenter(string server) =>
        ServerToDataCenter.GetValueOrDefault(server);
}
