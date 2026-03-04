using Microsoft.EntityFrameworkCore;

namespace MyApp.BuildingBlocks.Infrastructure.Observability.Persistence;

internal sealed class ObservabilityDbContext(DbContextOptions<ObservabilityDbContext> options)
	: DbContext(options)
{
	public DbSet<FailedHttpPayloadEntity> FailedHttpPayloads => Set<FailedHttpPayloadEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<FailedHttpPayloadEntity>(b =>
		{
			b.ToTable("FailedHttpPayload");
			b.HasKey(x => x.Id);

			b.Property(x => x.CreatedAtUtc).IsRequired();
			b.Property(x => x.CorrelationId).HasMaxLength(128);
			b.Property(x => x.TraceId).HasMaxLength(64);
			b.Property(x => x.SpanId).HasMaxLength(32);
			b.Property(x => x.RequestId).HasMaxLength(128);

			b.Property(x => x.Method).HasMaxLength(16);
			b.Property(x => x.Path).HasMaxLength(2048);
			b.Property(x => x.StatusCode);

			b.Property(x => x.RequestContentType).HasMaxLength(256);
			b.Property(x => x.ResponseContentType).HasMaxLength(256);

			b.Property(x => x.UserId).HasMaxLength(128);
			b.Property(x => x.RemoteIp).HasMaxLength(64);

			// Body/Headers mogą być duże
			b.Property(x => x.HeadersJson);
			b.Property(x => x.RequestBody);
			b.Property(x => x.ResponseBody);
		});
	}
}

internal sealed class FailedHttpPayloadEntity
{
	public long Id { get; set; }
	public DateTime CreatedAtUtc { get; set; }

	public string? CorrelationId { get; set; }
	public string? TraceId { get; set; }
	public string? SpanId { get; set; }
	public string? RequestId { get; set; }

	public string? Method { get; set; }
	public string? Path { get; set; }
	public int StatusCode { get; set; }

	public string? RequestContentType { get; set; }
	public string? ResponseContentType { get; set; }

	public string? UserId { get; set; }
	public string? RemoteIp { get; set; }

	public string? HeadersJson { get; set; }
	public string? RequestBody { get; set; }
	public string? ResponseBody { get; set; }
}