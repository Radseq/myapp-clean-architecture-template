using MyApp.Api.Middleware;
using NLog;
using System.Diagnostics;

namespace MyApp.Api.Logging;

public static class LoggingDiagnosticsEndpoints
{
    public static WebApplication MapLoggingDiagnostics(this WebApplication app)
    {
        var enabled =
            app.Environment.IsDevelopment() ||
            app.Configuration.GetValue<bool>("Observability:Logging:DiagnosticsEnabled");

        if (!enabled)
            return app;

        app.MapGet("/health/logging", (HttpContext ctx, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("LoggingDiagnostics");

            var cid =
                CorrelationIdMiddleware.TryGet(ctx)
                ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

            log.LogInformation("Logging diagnostics ping. CorrelationId={CorrelationId}", cid);

            var cfg = LogManager.Configuration;
            var targets = cfg?.AllTargets
                .Select(t => new { t.Name, Type = t.GetType().Name })
                .ToArray() ?? [];

            return Results.Ok(new
            {
                ok = true,
                app = Environment.GetEnvironmentVariable("MYAPP_APP"),
                env = Environment.GetEnvironmentVariable("MYAPP_ENV"),
                correlationId = cid,
                traceId = Activity.Current?.TraceId.ToString(),
                spanId = Activity.Current?.SpanId.ToString(),
                fileEnabled = Environment.GetEnvironmentVariable("MYAPP_LOG_FILE_ENABLED"),
                logDir = Environment.GetEnvironmentVariable("MYAPP_LOG_DIR"),
                targets
            });
        })
        .WithTags("Health");

        return app;
    }
}
