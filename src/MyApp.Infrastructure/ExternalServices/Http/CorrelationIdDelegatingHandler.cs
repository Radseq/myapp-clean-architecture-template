using MyApp.Application.Abstractions.Observability;

namespace MyApp.Infrastructure.ExternalServices.Http;

public sealed class CorrelationIdDelegatingHandler(ICorrelationContext ctx) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cid = ctx.CorrelationId;

        if (!string.IsNullOrWhiteSpace(cid))
        {
            request.Headers.Remove(CorrelationHeaders.HeaderName);
            request.Headers.TryAddWithoutValidation(CorrelationHeaders.HeaderName, cid);
        }

        return base.SendAsync(request, cancellationToken);
    }
}