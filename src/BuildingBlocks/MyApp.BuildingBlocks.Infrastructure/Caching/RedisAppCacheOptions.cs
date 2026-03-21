using System.Text.Json;

namespace MyApp.BuildingBlocks.Infrastructure.Caching;

public sealed class RedisAppCacheOptions
{
	public string? KeyPrefix { get; init; } = "myapp";
	public JsonSerializerOptions? JsonOptions { get; init; }
}
