namespace FFXIV.Domain.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
}
