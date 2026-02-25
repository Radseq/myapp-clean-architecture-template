using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions.Observability;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;
using System.Text.Json;

namespace MyApp.Infrastructure.Persistence.Repositories.Observability;

internal sealed class HttpPayloadStoreWriteRepository(IServiceScopeFactory scopeFactory,
    ILogger<HttpPayloadStoreWriteRepository> logger) : IFailedHttpPayloadStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // best-effort cleanup: odpalaj np. co 100 insertów (żeby nie robić delete przy każdym błędzie)
    private static int _cleanupTick;

    public async Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var headersJson = payload.Headers is null
                ? null
                : JsonSerializer.Serialize(payload.Headers, JsonOptions);

            var entity = new MicrosoftSqlServer.DbFirst.Entities.FailedHttpPayload
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

            // Opcjonalny retention cleanup (best-effort). Jeśli nie chcesz, usuń blok.
            if (ttl > TimeSpan.Zero && (Interlocked.Increment(ref _cleanupTick) % 100) == 0)
            {
                try
                {
                    var threshold = DateTime.UtcNow - ttl;

                    await db.Set<Persistence.MicrosoftSqlServer.DbFirst.Entities.FailedHttpPayload>()
                        .Where(x => x.CreatedAtUtc < threshold)
                        .ExecuteDeleteAsync(ct);
                }
                catch
                {
                    // NEVER throw
                }
            }
        }
        catch (Exception ex)
        {
            // best-effort: nie wywal requestu przez logowanie
            logger.LogDebug(ex, "Failed to store FailedHttpPayload (best-effort).");
        }
    }
}