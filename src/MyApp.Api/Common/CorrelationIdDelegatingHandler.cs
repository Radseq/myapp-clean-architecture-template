using MyApp.Api.Middleware;

namespace MyApp.Api.Common;

public sealed class CorrelationIdDelegatingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ctx = accessor.HttpContext;

        var cid = ctx is null
            ? null
            : (CorrelationIdMiddleware.TryGet(ctx)
               ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault());

        if (!string.IsNullOrWhiteSpace(cid))
        {
            request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, cid);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

