using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class SavedList
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SavedListType Type { get; set; }
    public string Data { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
