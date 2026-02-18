using MyApp.Application.Abstractions.Outbox;
using MyApp.Infrastructure.Persistence.DbFirst;
using MyApp.Infrastructure.Persistence.DbFirst.Entities;
using MyApp.Infrastructure.Persistence.DbFirst.Enums;

namespace MyApp.Infrastructure.Outbox;

public sealed class EfOutboxWriter(AppDbContext db, TimeProvider timeProvider) : IOutboxWriter
{
    public Guid EnqueuePending(string type, string idempotencyKey, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var msg = Pending(type, idempotencyKey, payloadJson, utcNow);

        db.OutboxMessages.Add(msg);
        return msg.Id;
    }

    private static OutboxMessage Pending(
    string type,
    string idempotencyKey,
    string payloadJson,
    DateTime utcNow)
    => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        IdempotencyKey = idempotencyKey,
        PayloadJson = payloadJson,
        Status = (byte)OutboxStatus.Pending,
        AttemptCount = 0,
        CreatedUtc = utcNow,
        NextAttemptUtc = utcNow
    };
}
