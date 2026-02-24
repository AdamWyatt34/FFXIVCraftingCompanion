namespace FFXIV.Domain.Entities;

public class Feedback
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PageUrl { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
