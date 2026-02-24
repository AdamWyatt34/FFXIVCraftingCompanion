namespace FFXIV.Application.SavedLists.DTOs;

public sealed class AddItemToSavedListRequest
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Category { get; set; }
}
