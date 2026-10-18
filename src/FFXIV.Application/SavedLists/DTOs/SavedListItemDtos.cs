using System.ComponentModel.DataAnnotations;

namespace FFXIV.Application.SavedLists.DTOs;

public sealed class AddItemToSavedListRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ItemId must be a positive integer.")]
    public int ItemId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "ItemName is required.")]
    [MaxLength(256, ErrorMessage = "ItemName must be 256 characters or fewer.")]
    public string ItemName { get; set; } = string.Empty;

    [Range(1, SavedListLimits.MaxQuantity, ErrorMessage = "Quantity must be between 1 and 999.")]
    public int Quantity { get; set; } = 1;

    [MaxLength(64, ErrorMessage = "Category must be 64 characters or fewer.")]
    public string? Category { get; set; }
}
