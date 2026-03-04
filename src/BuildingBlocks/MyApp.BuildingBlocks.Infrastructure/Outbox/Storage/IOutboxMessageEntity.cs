namespace MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

public interface IOutboxMessageEntity
{
	Guid Id { get; set; }
	string Type { get; set; }
	string PayloadJson { get; set; }
	string IdempotencyKey { get; set; }

	byte Status { get; set; }
	int AttemptCount { get; set; }

	DateTime? NextAttemptUtc { get; set; }
	DateTime? LockedUntilUtc { get; set; }
	Guid? LockId { get; set; }

	string? LastError { get; set; }
	DateTime CreatedUtc { get; set; }
	DateTime? ProcessedUtc { get; set; }

	byte[] RowVersion { get; set; }
	string CorrelationId { get; set; }
}