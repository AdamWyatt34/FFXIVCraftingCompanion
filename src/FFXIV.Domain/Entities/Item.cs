using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class Item
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public int ItemLevel { get; set; }
    public ItemCategory Category { get; set; }
    public bool CanBeHQ { get; set; }

    public ICollection<ItemSource> Sources { get; set; } = new List<ItemSource>();
    public ICollection<GatheringInfo> GatheringInfos { get; set; } = new List<GatheringInfo>();
    public ICollection<VendorInfo> VendorInfos { get; set; } = new List<VendorInfo>();
}
