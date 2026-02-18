using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MyApp.Api.Middleware;

public static class LogEvents
{
    public static readonly EventId RequestFinished = new(1001, nameof(RequestFinished));
}

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    IOptions<RequestLoggingOptions> options)
{
    public async Task Invoke(HttpContext ctx)
    {
        var start = Stopwatch.GetTimestamp();

        try
        {
            await next(ctx);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            var status = ctx.Response?.StatusCode ?? 0;

            var level =
                status >= 500 ? LogLevel.Error :
                status >= 400 ? LogLevel.Warning :
                LogLevel.Information;

            var endpoint = ctx.GetEndpoint()?.DisplayName;

            var opt = options.Value;

            var denyQuery = new HashSet<string>(opt.QueryStringDenyList ?? [], StringComparer.OrdinalIgnoreCase);
            var query = opt.LogQueryString
                ? LoggingRedaction.SanitizeQueryString(ctx.Request.Query, denyQuery, opt.MaxValueLength)
                : string.Empty;

            Dictionary<string, string>? headers = null;
            if (opt.LogHeaders)
            {
                var allow = new HashSet<string>(opt.HeaderAllowList ?? [], StringComparer.OrdinalIgnoreCase);
                var deny = new HashSet<string>(opt.HeaderDenyList ?? [], StringComparer.OrdinalIgnoreCase);
                headers = LoggingRedaction.SanitizeHeaders(ctx.Request.Headers, allow, deny, opt.MaxValueLength);
            }

            logger.Log(
                level,
                LogEvents.RequestFinished,
                // "@Headers" -> NLog.Extensions.Logging potrafi to ustrukturyzować gdy CaptureMessageTemplates=true
                "HTTP {Method} {Path}{Query} => {StatusCode} in {ElapsedMs} ms (Endpoint={Endpoint}) Headers={@Headers}",
                ctx.Request.Method,
                ctx.Request.Path.Value,
                query,
                status,
                elapsed.TotalMilliseconds,
                endpoint,
                headers);
        }
    }
}
