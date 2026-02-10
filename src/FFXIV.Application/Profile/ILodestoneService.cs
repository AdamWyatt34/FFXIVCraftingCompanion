namespace FFXIV.Application.Profile;

public interface ILodestoneService
{
    Task<LodestoneCharacterResult?> SearchAndFetchCharacterAsync(
        string characterName, string serverName, CancellationToken ct = default);

    Task<LodestoneCharacterResult?> FetchCharacterByIdAsync(
        string lodestoneId, CancellationToken ct = default);
}

public class LodestoneCharacterResult
{
    public string LodestoneId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string? DataCenter { get; set; }
    public string? AvatarUrl { get; set; }

    public string? ActiveJobAbbreviation { get; set; }
    public int ActiveJobLevel { get; set; }

    public Dictionary<string, int> JobLevels { get; set; } = new();
    public List<LodestoneGearPiece> EquippedGear { get; set; } = new();
    public LodestoneAttributes? Attributes { get; set; }
}

public class LodestoneGearPiece
{
    public string SlotName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public bool IsHQ { get; set; }
    public string? GlamourName { get; set; }
    public int ItemLevel { get; set; }
}

public class LodestoneAttributes
{
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }
}
