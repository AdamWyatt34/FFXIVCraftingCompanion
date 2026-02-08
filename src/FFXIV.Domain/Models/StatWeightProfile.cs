namespace FFXIV.Domain.Models;

public class StatWeightProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double CraftsmanshipWeight { get; set; }
    public double ControlWeight { get; set; }
    public double CpWeight { get; set; }
    public double GatheringWeight { get; set; }
    public double PerceptionWeight { get; set; }
    public double GpWeight { get; set; }
}
