using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using MyApp.Application.Abstractions.Observability;

namespace MyApp.Infrastructure.Observability;

public sealed class RedisFailedHttpPayloadStore(IDistributedCache cache) : IFailedHttpPayloadStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);

            await cache.SetStringAsync(
                payload.Key,
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);
        }
        catch
        {
            // NEVER throw from diagnostics store
        }
    }
}
