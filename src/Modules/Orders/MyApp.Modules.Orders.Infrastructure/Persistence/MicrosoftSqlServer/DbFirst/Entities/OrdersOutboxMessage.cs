using MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

namespace MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

public partial class OrdersOutboxMessage : IOutboxMessageEntity
{
	public Guid Id { get; set; }

	public string Type { get; set; } = null!;

	public string PayloadJson { get; set; } = null!;

	public string IdempotencyKey { get; set; } = null!;

	public byte Status { get; set; }

	public int AttemptCount { get; set; }

	public DateTime? NextAttemptUtc { get; set; }

	public DateTime? LockedUntilUtc { get; set; }

	public Guid? LockId { get; set; }

	public string? LastError { get; set; }

	public DateTime CreatedUtc { get; set; }

	public DateTime? ProcessedUtc { get; set; }

	public byte[] RowVersion { get; set; } = null!;

	public string CorrelationId { get; set; } = null!;
}
