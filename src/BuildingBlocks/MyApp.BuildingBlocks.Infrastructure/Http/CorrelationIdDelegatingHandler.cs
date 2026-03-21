using MyApp.BuildingBlocks.Application.Abstractions.Observability;

namespace MyApp.BuildingBlocks.Infrastructure.Http;

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