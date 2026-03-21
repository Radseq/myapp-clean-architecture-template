namespace MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

public partial class FailedHttpPayload
{
	public long Id { get; set; }

	public DateTime CreatedAtUtc { get; set; }

	public string? CorrelationId { get; set; }

	public string? TraceId { get; set; }

	public string? SpanId { get; set; }

	public string? RequestId { get; set; }

	public string Method { get; set; } = null!;

	public string Path { get; set; } = null!;

	public int StatusCode { get; set; }

	public string? RequestContentType { get; set; }

	public string? ResponseContentType { get; set; }

	public string? UserId { get; set; }

	public string? RemoteIp { get; set; }

	public string? HeadersJson { get; set; }

	public string? RequestBody { get; set; }

	public string? ResponseBody { get; set; }
}
