using MyApp.BuildingBlocks.Application.Abstractions.Observability;

namespace MyApp.BuildingBlocks.Infrastructure.Observability;

public sealed class NullFailedHttpPayloadStore : IFailedHttpPayloadStore
{
    public Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct)
        => Task.CompletedTask;
}
