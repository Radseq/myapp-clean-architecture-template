namespace MyApp.Application.Abstractions.Outbox;

public sealed record OutboxEnvelope(
    Guid Id,
    string Type,
    string PayloadJson,
    string IdempotencyKey,
    int AttemptCount);

public enum OutboxDispatchStatus
{
    Done,
    Retry,
    Dead
}

public sealed record OutboxDispatchResult(
    OutboxDispatchStatus Status,
    string? LastError = null)
{
    public static OutboxDispatchResult Done() => new(OutboxDispatchStatus.Done);
    public static OutboxDispatchResult Retry(string? error = null) => new(OutboxDispatchStatus.Retry, error);
    public static OutboxDispatchResult Dead(string? error = null) => new(OutboxDispatchStatus.Dead, error);
}

/// <summary>
/// Handler konkretnego typu outbox message. Implementacje zwykle siedzą w Infrastructure (bo HttpClient).
/// </summary>
public interface IOutboxMessageHandler
{
    string Type { get; }
    Task<OutboxDispatchResult> DispatchAsync(OutboxEnvelope envelope, CancellationToken ct);
}

public interface IOutboxDispatcher
{
    Task<bool> TryDispatchOnceAsync(Guid outboxId, CancellationToken ct);
}
