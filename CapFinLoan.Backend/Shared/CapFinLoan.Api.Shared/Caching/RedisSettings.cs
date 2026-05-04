namespace CapFinLoan.Api.Shared.Caching;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";
    public string ConnectionString { get; init; } = "localhost:6379";
    public int DefaultExpiryMinutes { get; init; } = 5;
}
