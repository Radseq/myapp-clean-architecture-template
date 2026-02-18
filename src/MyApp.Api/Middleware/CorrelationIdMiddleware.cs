using System.Diagnostics;

namespace MyApp.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /*
        Sklejanie logów z wielu usług w jeden “timeline” – szukasz po CorrelationId i widzisz: API → DB → zewnętrzne API → ewentualne retry → itd.
        Obsługa produkcji/support: klient dostaje X-Correlation-ID w odpowiedzi (nawet dla 4xx/5xx) i zgłasza Ci to ID → Ty od razu znajdujesz wszystkie logi.
        Łatwe debugowanie błędów częściowych (partial success) – np. “order utworzone, ale dispatch do transportu nie wyszedł”: ostrzeżenie zawiera correlationId i możesz prześledzić oba systemy.
        Różnica względem TraceId: TraceId jest z distributed tracing (Activity/OpenTelemetry, nagłówek traceparent). To też działa cross-service, ale correlationId jest prostsze dla ludzi i dla klientów z zewnątrz (którzy nie mają traceparent). Najlepiej mieć oba: TraceId do analizy technicznej, CorrelationId do spajania logów/supportu.
     */

    public const string HeaderName = "X-Correlation-ID";
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

    public async Task Invoke(HttpContext ctx)
    {
        var correlationId = GetOrCreateCorrelationId(ctx);

        // Dzięki temu correlationId będzie widoczne też w narzędziach tracingowych (Grafana/Tempo/Jaeger), nie tylko w logach.
        Activity.Current?.SetBaggage("correlation_id", correlationId);
        Activity.Current?.AddTag("correlation_id", correlationId);

        ctx.Items[ItemKey] = correlationId;

        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = correlationId,
            ["traceId"] = traceId,
            ["spanId"] = spanId,
            ["requestId"] = ctx.TraceIdentifier
        }))
        {
            await next(ctx);
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
