using MyApp.Application.Abstractions.Observability;

namespace MyApp.Infrastructure.Observability;

public sealed class NullFailedHttpPayloadStore : IFailedHttpPayloadStore
{
    public Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct)
        => Task.CompletedTask;
}
