using Microsoft.Data.SqlClient;
using MyApp.Application.Abstractions.Observability;
using System.Text.Json;

namespace MyApp.Infrastructure.Observability;

public sealed class SqlFailedHttpPayloadStore(string connectionString) : IFailedHttpPayloadStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task TryStoreAsync(FailedHttpPayload payload, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            const string sql = """
INSERT INTO dbo.FailedHttpPayload
(
  CreatedAtUtc, CorrelationId, TraceId, SpanId, RequestId,
  Method, Path, StatusCode,
  RequestContentType, ResponseContentType,
  UserId, RemoteIp,
  HeadersJson, RequestBody, ResponseBody
)
VALUES
(
  @CreatedAtUtc, @CorrelationId, @TraceId, @SpanId, @RequestId,
  @Method, @Path, @StatusCode,
  @RequestContentType, @ResponseContentType,
  @UserId, @RemoteIp,
  @HeadersJson, @RequestBody, @ResponseBody
);
""";

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@CreatedAtUtc", payload.CreatedAtUtc.UtcDateTime);
            cmd.Parameters.AddWithValue("@CorrelationId", (object?)payload.CorrelationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TraceId", (object?)payload.TraceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SpanId", (object?)payload.SpanId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RequestId", (object?)payload.RequestId ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@Method", payload.Method);
            cmd.Parameters.AddWithValue("@Path", payload.Path);
            cmd.Parameters.AddWithValue("@StatusCode", payload.StatusCode);

            cmd.Parameters.AddWithValue("@RequestContentType", (object?)payload.RequestContentType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ResponseContentType", (object?)payload.ResponseContentType ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@UserId", (object?)payload.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RemoteIp", (object?)payload.RemoteIp ?? DBNull.Value);

            var headersJson = payload.Headers is null ? null : JsonSerializer.Serialize(payload.Headers, JsonOptions);
            cmd.Parameters.AddWithValue("@HeadersJson", (object?)headersJson ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@RequestBody", (object?)payload.RequestBody ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ResponseBody", (object?)payload.ResponseBody ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // NEVER throw
        }
    }
}
