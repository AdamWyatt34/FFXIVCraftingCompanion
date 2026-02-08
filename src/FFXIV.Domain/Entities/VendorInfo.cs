namespace FFXIV.Domain.Entities;

public class VendorInfo
{
    public int VendorInfoId { get; set; }
    public int ItemId { get; set; }
    public string NpcName { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public float? CoordinateX { get; set; }
    public float? CoordinateY { get; set; }
    public int Cost { get; set; }
    public string CurrencyType { get; set; } = "Gil";
    public int? CurrencyItemId { get; set; }

    public Item Item { get; set; } = null!;
    public Item? CurrencyItem { get; set; }
}
