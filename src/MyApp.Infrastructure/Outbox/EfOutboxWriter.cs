using MyApp.Application.Abstractions.Outbox;
using MyApp.Application.Abstractions.Observability;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Enums;

namespace MyApp.Infrastructure.Outbox;

public sealed class EfOutboxWriter(
    AppDbContext db,
    TimeProvider timeProvider,
    ICorrelationContext correlation) : IOutboxWriter
{
    public Guid EnqueuePending(string type, string idempotencyKey, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Opcja A: zapisujemy correlationId do outboxa (spina request -> retry z workera)
        var cid = SanitizeCorrelationId(correlation.CorrelationId);

        var msg = Pending(type, idempotencyKey, payloadJson, utcNow, cid);

        // fallback (przydatne gdy enqueue dzieje siê bez requestu)
        msg.CorrelationId ??= msg.Id.ToString("N");

        db.OutboxMessages.Add(msg);
        return msg.Id;
    }

    private static OutboxMessage Pending(
        string type,
        string idempotencyKey,
        string payloadJson,
        DateTime utcNow,
        string? correlationId)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            IdempotencyKey = idempotencyKey,
            PayloadJson = payloadJson,
            Status = (byte)OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedUtc = utcNow,
            NextAttemptUtc = utcNow,

            CorrelationId = correlationId
        };

    private static string? SanitizeCorrelationId(string? cid)
    {
        if (string.IsNullOrWhiteSpace(cid))
            return null;

        cid = cid.Trim();

        // spójne z middleware: limit d³ugoœci ¿eby nie zabiæ logów / db
        return cid.Length <= 128 ? cid : cid.Substring(0, 128);
    }
}