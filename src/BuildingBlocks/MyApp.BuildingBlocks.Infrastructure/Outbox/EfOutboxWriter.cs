using Microsoft.EntityFrameworkCore;
using MyApp.BuildingBlocks.Application.Abstractions.Observability;
using MyApp.BuildingBlocks.Application.Abstractions.Outbox;
using MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

namespace MyApp.BuildingBlocks.Infrastructure.Outbox;

public sealed class EfOutboxWriter<TDbContext, TMsg, TModule>(
	TDbContext db,
	TimeProvider timeProvider,
	ICorrelationContext correlation)
	: IOutboxWriter<TModule>
	where TDbContext : DbContext
	where TMsg : class, IOutboxMessageEntity, new()
{
	public Guid EnqueuePending(string type, string idempotencyKey, string payloadJson)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(type);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

		var utcNow = timeProvider.GetUtcNow().UtcDateTime;
		var cid = SanitizeCorrelationId(correlation.CorrelationId);

		var msg = new TMsg
		{
			Id = Guid.NewGuid(),
			Type = type,
			IdempotencyKey = idempotencyKey,
			PayloadJson = payloadJson,
			Status = OutboxStatusCodes.Pending,
			AttemptCount = 0,
			CreatedUtc = utcNow,
			NextAttemptUtc = utcNow,
			CorrelationId = cid ?? string.Empty
		};

		if (string.IsNullOrWhiteSpace(msg.CorrelationId))
			msg.CorrelationId = msg.Id.ToString("N");

		db.Set<TMsg>().Add(msg);
		return msg.Id;
	}

	private static string? SanitizeCorrelationId(string? cid)
	{
		if (string.IsNullOrWhiteSpace(cid))
			return null;

		cid = cid.Trim();
		return cid.Length <= 128 ? cid : cid[..128];
	}
}