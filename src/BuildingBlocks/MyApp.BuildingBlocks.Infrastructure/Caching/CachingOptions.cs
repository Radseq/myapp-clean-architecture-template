namespace MyApp.BuildingBlocks.Infrastructure.Caching;

public sealed class CachingOptions
{
    public bool UseRedis { get; init; }
    public string? KeyPrefix { get; init; } = "myapp";
}
