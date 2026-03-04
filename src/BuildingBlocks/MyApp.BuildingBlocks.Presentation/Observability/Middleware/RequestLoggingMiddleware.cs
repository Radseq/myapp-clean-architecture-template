using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.BuildingBlocks.Presentation.Observability.Options;
using MyApp.BuildingBlocks.Presentation.Observability.Redaction;
using System.Collections.Frozen;
using System.Diagnostics;

namespace MyApp.BuildingBlocks.Presentation.Observability.Middleware;

public static class LogEvents
{
    public static readonly EventId RequestFinished = new(1001, nameof(RequestFinished));
    public static readonly EventId UnhandledException = new(1002, nameof(UnhandledException));
    public static readonly EventId BodyCaptured = new(1003, nameof(BodyCaptured));
}

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    // snapshot bez alokacji HashSet per-request, czyli bez alloc per request
    private volatile Snapshot _snapshot;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptionsMonitor<RequestLoggingOptions> options)
    {
        _next = next;
        _logger = logger;

        _snapshot = Snapshot.From(options.CurrentValue);

        options.OnChange((o, _) => _snapshot = Snapshot.From(o));
    }

    public async Task Invoke(HttpContext ctx)
    {
        var start = Stopwatch.GetTimestamp();

        try
        {
            await _next(ctx);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            var status = ctx.Response?.StatusCode ?? 0;

            var level = DecideLevel(status);
            var endpoint = ctx.GetEndpoint()?.DisplayName;

            var snap = _snapshot;

            var query = snap.LogQueryString
                ? LoggingRedaction.SanitizeQueryString(ctx.Request.Query, snap.QueryStringDeny, snap.MaxValueLength)
                : string.Empty;

            if (snap.LogHeaders)
            {
                var headers = LoggingRedaction.SanitizeHeaders(
                    ctx.Request.Headers,
                    snap.HeaderAllowList,
                    snap.HeaderDenyList,
                    snap.MaxValueLength);

                _logger.Log(
                    level,
                    LogEvents.RequestFinished,
                    "HTTP {Method} {Path}{Query} => {StatusCode} in {ElapsedMs} ms (Endpoint={Endpoint}) (Headers={Headers})",
                    ctx.Request.Method,
                    ctx.Request.Path.Value,
                    query,
                    status,
                    elapsed.TotalMilliseconds,
                    endpoint,
                    headers);
            }
            else
            {
                _logger.Log(
                    level,
                    LogEvents.RequestFinished,
                    "HTTP {Method} {Path}{Query} => {StatusCode} in {ElapsedMs} ms (Endpoint={Endpoint})",
                    ctx.Request.Method,
                    ctx.Request.Path.Value,
                    query,
                    status,
                    elapsed.TotalMilliseconds,
                    endpoint);
            }
        }
    }

    private static LogLevel DecideLevel(int statusCode)
    {
        if (statusCode >= 500)
            return LogLevel.Error;

        // 429 to zwykle “ważniejszy” sygnał operacyjny
        if (statusCode == StatusCodes.Status429TooManyRequests)
            return LogLevel.Warning;

        // większość 4xx to “normalny traffic” (walidacje, 401/403 itd.)
        if (statusCode >= 400)
            return LogLevel.Information;

        return LogLevel.Information;
    }

    private sealed record class Snapshot(
        bool LogQueryString,
        bool LogHeaders,
        int MaxValueLength,
        FrozenSet<string> HeaderAllowList,
        FrozenSet<string> HeaderDenyList,
        FrozenSet<string> QueryStringDeny)
    {
        // przy częstych zmianach allow/deny list(np.feature flags “na żywo”): jepiej użyć HashSet dla heavy zostać przy FrozenSet

        public static Snapshot From(RequestLoggingOptions o)
        {
            var allow = (o.HeaderAllowList ?? Array.Empty<string>()).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            var deny = (o.HeaderDenyList ?? Array.Empty<string>()).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            var qdeny = (o.QueryStringDenyList ?? Array.Empty<string>()).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            return new Snapshot(
                LogQueryString: o.LogQueryString,
                LogHeaders: o.LogHeaders,
                MaxValueLength: Math.Max(16, o.MaxValueLength),
                HeaderAllowList: allow,
                HeaderDenyList: deny,
                QueryStringDeny: qdeny);
        }
    }
}