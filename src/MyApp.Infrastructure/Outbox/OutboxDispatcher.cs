using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions.Outbox;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Enums;
namespace MyApp.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    AppDbContext db,
    IEnumerable<IOutboxMessageHandler> handlers,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger)
    : IOutboxDispatcher
{
    private readonly OutboxOptions _opt = options.Value;
    private readonly Dictionary<string, IOutboxMessageHandler> _map = handlers
        .GroupBy(h => h.Type, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public async Task<bool> TryDispatchOnceAsync(Guid outboxId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lockId = Guid.NewGuid();
        var leaseUntil = now + _opt.LeaseTime;

        // acquire single
        var msg = await AcquireSingleAsync(outboxId, now, lockId, leaseUntil, ct);
        if (msg is null)
            return false;

        var envelope = new OutboxEnvelope(msg.Id, msg.Type, msg.PayloadJson, msg.IdempotencyKey, msg.AttemptCount);

        OutboxDispatchResult result;
        if (!_map.TryGetValue(msg.Type, out var handler))
        {
            result = OutboxDispatchResult.Dead($"No outbox handler registered for type '{msg.Type}'.");
        }
        else
        {
            try
            {
                result = await handler.DispatchAsync(envelope, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch exception. Id={Id} Type={Type}", msg.Id, msg.Type);
                result = OutboxDispatchResult.Retry(ex.Message);
            }
        }

        await ApplyResultAsync(msg.Id, lockId, result, ct);
        return result.Status == OutboxDispatchStatus.Done;
    }

    private async Task<OutboxMessage?> AcquireSingleAsync(
        Guid id,
        DateTime nowUtc,
        Guid lockId,
        DateTime leaseUntilUtc,
        CancellationToken ct)
    {
        var executionStrategy = db.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var msg = await db.OutboxMessages
                .SingleOrDefaultAsync(m =>
                    m.Id == id &&
                    (m.Status == (byte)OutboxStatus.Pending || m.Status == (byte)OutboxStatus.Failed) &&
                    (m.NextAttemptUtc == null || m.NextAttemptUtc <= nowUtc) &&
                    (m.LockedUntilUtc == null || m.LockedUntilUtc < nowUtc), ct);

            if (msg is null)
                return null;

            msg.Status = (byte)OutboxStatus.Processing;
            msg.LockId = lockId;
            msg.LockedUntilUtc = leaseUntilUtc;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return msg;
        });
    }

    private async Task ApplyResultAsync(Guid id, Guid lockId, OutboxDispatchResult result, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var executionStrategy = db.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var entity = await db.OutboxMessages
                .SingleOrDefaultAsync(m => m.Id == id && m.LockId == lockId, ct);

            if (entity is null)
                return;

            entity.LockedUntilUtc = null;
            entity.LockId = null;

            switch (result.Status)
            {
                case OutboxDispatchStatus.Done:
                    entity.Status = (byte)OutboxStatus.Done;
                    entity.ProcessedUtc = now;
                    entity.LastError = null;
                    entity.NextAttemptUtc = null;
                    break;

                case OutboxDispatchStatus.Dead:
                    entity.Status = (byte)OutboxStatus.Dead;
                    entity.ProcessedUtc = now;
                    entity.LastError = Truncate(result.LastError, 2000);
                    entity.NextAttemptUtc = null;
                    break;

                case OutboxDispatchStatus.Retry:
                default:
                    entity.AttemptCount++;
                    entity.LastError = Truncate(result.LastError, 2000);

                    if (entity.AttemptCount >= _opt.MaxAttempts)
                    {
                        entity.Status = (byte)OutboxStatus.Dead;
                        entity.ProcessedUtc = now;
                        entity.NextAttemptUtc = null;
                    }
                    else
                    {
                        entity.Status = (byte)OutboxStatus.Failed;
                        entity.ProcessedUtc = null;
                        entity.NextAttemptUtc = now + ComputeBackoff(entity.AttemptCount);
                    }
                    break;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var mult = 1 << Math.Min(attempt - 1, 10);
        var backoff = TimeSpan.FromTicks(_opt.MinBackoff.Ticks * mult);
        return backoff <= _opt.MaxBackoff ? backoff : _opt.MaxBackoff;
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
