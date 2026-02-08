namespace FFXIV.Domain.Models;

public class GearSet
{
    public string Job { get; set; } = string.Empty;
    public int Level { get; set; }
    public StatWeightProfile Profile { get; set; } = new();
    public List<GearSlotRecommendation> Slots { get; set; } = [];
    public GearStats TotalStats { get; set; } = new();
    public double CompletionPercentage { get; set; }
}
