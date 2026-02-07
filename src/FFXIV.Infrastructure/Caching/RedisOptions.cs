namespace FFXIV.Infrastructure.Caching;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "localhost:6379";
}
