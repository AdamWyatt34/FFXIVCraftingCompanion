namespace FFXIV.Domain.Models;

public class GearStats
{
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }
    public double TotalWeightedScore { get; set; }
}
