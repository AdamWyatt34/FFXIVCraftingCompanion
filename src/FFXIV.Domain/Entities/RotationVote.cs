namespace FFXIV.Domain.Entities;

public class RotationVote
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int RotationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public SharedRotation Rotation { get; set; } = null!;
}
