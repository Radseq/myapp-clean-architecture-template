using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.BuildingBlocks.Application.Abstractions.Observability;
using System.Text.Json;

namespace MyApp.BuildingBlocks.Infrastructure.Observability.Persistence;

internal sealed class HttpPayloadStoreWriteRepository(
	IServiceScopeFactory scopeFactory,
	ILogger<HttpPayloadStoreWriteRepository> logger) : IFailedHttpPayloadStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static int _cleanupTick;

	public async Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct)
	{
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<ObservabilityDbContext>();

			var headersJson = payload.Headers is null
				? null
				: JsonSerializer.Serialize(payload.Headers, JsonOptions);

			var entity = new FailedHttpPayloadEntity
			{
				CreatedAtUtc = payload.CreatedAtUtc.UtcDateTime,

				CorrelationId = payload.CorrelationId,
				TraceId = payload.TraceId,
				SpanId = payload.SpanId,
				RequestId = payload.RequestId,

				Method = payload.Method,
				Path = payload.Path,
				StatusCode = payload.StatusCode,

				RequestContentType = payload.RequestContentType,
				ResponseContentType = payload.ResponseContentType,

				UserId = payload.UserId,
				RemoteIp = payload.RemoteIp,

				HeadersJson = headersJson,
				RequestBody = payload.RequestBody,
				ResponseBody = payload.ResponseBody
			};

			db.FailedHttpPayloads.Add(entity);
			await db.SaveChangesAsync(ct);

			if (ttl > TimeSpan.Zero && (Interlocked.Increment(ref _cleanupTick) % 100) == 0)
			{
				try
				{
					var threshold = DateTime.UtcNow - ttl;
					await db.FailedHttpPayloads
						.Where(x => x.CreatedAtUtc < threshold)
						.ExecuteDeleteAsync(ct);
				}
				catch { /* best-effort */ }
			}
		}
		catch (Exception ex)
		{
			logger.LogDebug(ex, "Failed to store FailedHttpPayload (best-effort).");
		}
	}
}