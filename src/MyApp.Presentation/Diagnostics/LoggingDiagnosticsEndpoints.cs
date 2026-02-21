using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Presentation.Observability.Middleware;
using System.Diagnostics;

namespace MyApp.Presentation.Diagnostics;

public static class LoggingDiagnosticsEndpoints
{
    public static WebApplication MapLoggingDiagnostics(this WebApplication app)
    {
        var enabled =
            app.Environment.IsDevelopment() ||
            app.Configuration.GetValue<bool>("Observability:Logging:DiagnosticsEnabled");

        if (!enabled)
            return app;

        app.MapGet("/health/logging", (
            HttpContext ctx,
            ILoggerFactory lf,
            ILoggingDiagnosticsProvider? provider) =>
        {
            var log = lf.CreateLogger("LoggingDiagnostics");

            var cid =
                CorrelationIdMiddleware.TryGet(ctx)
                ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

            log.LogInformation("Logging diagnostics ping. CorrelationId={CorrelationId}", cid);

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
                provider = provider?.GetProviderInfo() ?? new { provider = "Unknown" }
            });
        })
        .WithTags("Health");

        return app;
    }
}