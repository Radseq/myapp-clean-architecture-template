using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.BuildingBlocks.Application.Abstractions.Observability;
using MyApp.BuildingBlocks.Application.Abstractions.Outbox;
using MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

namespace MyApp.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxProcessor<TDbContext, TMsg, TModule>(
	TDbContext db,
	IEnumerable<IOutboxMessageHandler<TModule>> handlers,
	IOptionsMonitor<OutboxOptions> options,
	IOutboxModuleOptions<TModule> moduleOpt,
	TimeProvider timeProvider,
	ILogger<OutboxProcessor<TDbContext, TMsg, TModule>> logger,
	ICorrelationContext correlation)
	where TDbContext : DbContext
	where TMsg : class, IOutboxMessageEntity
{
	private OutboxOptions Opt => options.Get(moduleOpt.OptionsName);

	private readonly Dictionary<string, IOutboxMessageHandler<TModule>> _map =
		handlers.GroupBy(h => h.Type, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

	public async Task<int> RunOnceAsync(CancellationToken ct)
	{
		var opt = Opt;

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var lockId = Guid.NewGuid();
		var leaseUntil = now + opt.LeaseTime;

		var batch = await AcquireBatchAsync(now, lockId, leaseUntil, opt.BatchSize, ct);
		if (batch.Count == 0)
			return 0;

		var processed = 0;

		foreach (var msg in batch)
		{
			ct.ThrowIfCancellationRequested();

			var cid = !string.IsNullOrWhiteSpace(msg.CorrelationId)
				? msg.CorrelationId
				: msg.Id.ToString("N");

			correlation.CorrelationId = cid;

			using var scope = logger.BeginScope(new Dictionary<string, object?>
			{
				["correlationId"] = cid,
				["outboxId"] = msg.Id,
				["outboxType"] = msg.Type,
				["attempt"] = msg.AttemptCount
			});

			try
			{
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
					catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
					catch (Exception ex)
					{
						logger.LogError(ex, "Outbox dispatch exception. Id={Id} Type={Type}", msg.Id, msg.Type);
						result = OutboxDispatchResult.Retry(ex.Message);
					}
				}

				await ApplyResultAsync(msg.Id, lockId, result, ct);
				processed++;
			}
			finally
			{
				correlation.CorrelationId = null; // MUST HAVE
			}
		}

		return processed;
	}

	private async Task<List<TMsg>> AcquireBatchAsync(
		DateTime nowUtc,
		Guid lockId,
		DateTime leaseUntilUtc,
		int batchSize,
		CancellationToken ct)
	{
		var strategy = db.Database.CreateExecutionStrategy();

		return await strategy.ExecuteAsync(async () =>
		{
			await using var tx = await db.Database.BeginTransactionAsync(ct);

			var set = db.Set<TMsg>();

			var q = set.Where(m =>
				(EF.Property<byte>(m, nameof(IOutboxMessageEntity.Status)) == OutboxStatusCodes.Pending ||
				 EF.Property<byte>(m, nameof(IOutboxMessageEntity.Status)) == OutboxStatusCodes.Failed) &&
				(EF.Property<DateTime?>(m, nameof(IOutboxMessageEntity.NextAttemptUtc)) == null ||
				 EF.Property<DateTime?>(m, nameof(IOutboxMessageEntity.NextAttemptUtc)) <= nowUtc) &&
				(EF.Property<DateTime?>(m, nameof(IOutboxMessageEntity.LockedUntilUtc)) == null ||
				 EF.Property<DateTime?>(m, nameof(IOutboxMessageEntity.LockedUntilUtc)) < nowUtc));

			var batch = await q
				.OrderBy(m => EF.Property<DateTime>(m, nameof(IOutboxMessageEntity.CreatedUtc)))
				.Take(batchSize)
				.ToListAsync(ct);

			if (batch.Count == 0)
				return batch;

			foreach (var m in batch)
			{
				m.Status = OutboxStatusCodes.Processing;
				m.LockId = lockId;
				m.LockedUntilUtc = leaseUntilUtc;
			}

			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);

			return batch;
		});
	}

	private async Task ApplyResultAsync(Guid id, Guid lockId, OutboxDispatchResult result, CancellationToken ct)
	{
		var opt = Opt;

		var now = timeProvider.GetUtcNow().UtcDateTime;
		var strategy = db.Database.CreateExecutionStrategy();

		await strategy.ExecuteAsync(async () =>
		{
			await using var tx = await db.Database.BeginTransactionAsync(ct);

			var entity = await db.Set<TMsg>()
				.SingleOrDefaultAsync(m =>
					EF.Property<Guid>(m, nameof(IOutboxMessageEntity.Id)) == id &&
					EF.Property<Guid?>(m, nameof(IOutboxMessageEntity.LockId)) == lockId, ct);

			if (entity is null)
				return;

			entity.LockedUntilUtc = null;
			entity.LockId = null;

			switch (result.Status)
			{
				case OutboxDispatchStatus.Done:
					entity.Status = OutboxStatusCodes.Done;
					entity.ProcessedUtc = now;
					entity.LastError = null;
					entity.NextAttemptUtc = null;
					break;

				case OutboxDispatchStatus.Dead:
					entity.Status = OutboxStatusCodes.Dead;
					entity.ProcessedUtc = now;
					entity.LastError = Truncate(result.LastError, 2000);
					entity.NextAttemptUtc = null;
					break;

				case OutboxDispatchStatus.Retry:
				default:
					entity.AttemptCount++;
					entity.LastError = Truncate(result.LastError, 2000);

					if (entity.AttemptCount >= opt.MaxAttempts)
					{
						entity.Status = OutboxStatusCodes.Dead;
						entity.ProcessedUtc = now;
						entity.NextAttemptUtc = null;
					}
					else
					{
						entity.Status = OutboxStatusCodes.Failed;
						entity.ProcessedUtc = null;
						entity.NextAttemptUtc = now + ComputeBackoff(opt, entity.AttemptCount);
					}
					break;
			}

			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
		});
	}

	private static TimeSpan ComputeBackoff(OutboxOptions opt, int attempt)
	{
		var mult = 1 << Math.Min(attempt - 1, 10);
		var backoff = TimeSpan.FromTicks(opt.MinBackoff.Ticks * mult);
		return backoff <= opt.MaxBackoff ? backoff : opt.MaxBackoff;
	}

	private static string? Truncate(string? s, int max)
		=> string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}