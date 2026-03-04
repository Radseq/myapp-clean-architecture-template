using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyApp.BuildingBlocks.Application.Abstractions.Observability;
using System.Diagnostics;

namespace MyApp.BuildingBlocks.Presentation.Observability.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /*
        Sklejanie logów z wielu usług w jeden “timeline” – szukasz po CorrelationId i widzisz: API → DB → zewnętrzne API → ewentualne retry → itd.
        Obsługa produkcji/support: klient dostaje X-Correlation-ID w odpowiedzi (nawet dla 4xx/5xx) i zgłasza Ci to ID → Ty od razu znajdujesz wszystkie logi.
        Łatwe debugowanie błędów częściowych (partial success) – np. “order utworzone, ale dispatch do transportu nie wyszedł”: ostrzeżenie zawiera correlationId i możesz prześledzić oba systemy.
        Różnica względem TraceId: TraceId jest z distributed tracing (Activity/OpenTelemetry, nagłówek traceparent). To też działa cross-service, ale correlationId jest prostsze dla ludzi i dla klientów z zewnątrz (którzy nie mają traceparent). Najlepiej mieć oba: TraceId do analizy technicznej, CorrelationId do spajania logów/supportu.
     */

    public const string HeaderName = CorrelationHeaders.HeaderName;

    public const string ItemKey = "__CorrelationId";

    // jak wykożystać correlation id w innych api
    /*
    public sealed class DemoController : ControllerBase
    {
        private readonly ILogger<DemoController> _logger;

        public DemoController(ILogger<DemoController> logger) => _logger = logger;

        [HttpPost("receive")]
        public IActionResult Receive()
        {
            var cid = CorrelationIdMiddleware.TryGet(HttpContext)
                      ?? Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

            _logger.LogInformation("Received request with CorrelationId={CorrelationId}", cid);

            return Ok(new { correlationId = cid });
        }
    }*/

    public async Task Invoke(HttpContext ctx, ICorrelationContext correlation)
    {
        correlation.CorrelationId = GetOrCreateCorrelationId(ctx);

        // Dzięki temu correlationId będzie widoczne też w narzędziach tracingowych (Grafana/Tempo/Jaeger), nie tylko w logach.
        Activity.Current?.SetBaggage("correlation_id", correlation.CorrelationId);
        Activity.Current?.AddTag("correlation_id", correlation.CorrelationId);

        ctx.Items[ItemKey] = correlation.CorrelationId;

        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderName] = correlation.CorrelationId;
            return Task.CompletedTask;
        });

        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = correlation.CorrelationId,
            ["traceId"] = traceId,
            ["spanId"] = spanId,
            ["requestId"] = ctx.TraceIdentifier
        }))
        try
        {
            await next(ctx);
        }
        finally
        {
            correlation.CorrelationId = null; // ważne – nie przeciekaj między requestami
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext ctx)
    {
        var fromHeader = ctx.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            // prosta sanitacja: limit długości, trim
            var sanitized = fromHeader.Trim();
            if (sanitized.Length <= 128)
                return sanitized;
        }

        return Guid.NewGuid().ToString("N");
    }

    public static string? TryGet(HttpContext ctx)
        => ctx.Items.TryGetValue(ItemKey, out var v) ? v?.ToString() : null;
}
