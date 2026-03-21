namespace MyApp.BuildingBlocks.Application.Abstractions.Observability;

public sealed record FailedHttpPayload(
	string Key,
	DateTimeOffset CreatedAtUtc,
	string? CorrelationId,
	string? TraceId,
	string? SpanId,
	string? RequestId,
	string Method,
	string Path,
	int StatusCode,
	string? RequestContentType,
	string? ResponseContentType,
	string? UserId,
	string? RemoteIp,
	IReadOnlyDictionary<string, string>? Headers,
	string? RequestBody,
	string? ResponseBody);

public interface IFailedHttpPayloadStore
{
	/// <summary>
	/// Best-effort store. Must NEVER throw to the caller (middleware).
	/// </summary>
	Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct);
}
