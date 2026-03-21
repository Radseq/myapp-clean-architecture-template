using Microsoft.AspNetCore.Http;
using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.BuildingBlocks.Presentation.Observability.Middleware;

public static class BodyLogPolicyHttpContext
{
	public const string ItemKey = "myapp.bodylog.policy";

	public static void Set(HttpContext ctx, BodyLogPolicy policy)
	{
		if (policy != BodyLogPolicy.Default)
			ctx.Items[ItemKey] = policy;
	}

	public static BodyLogPolicy Get(HttpContext ctx)
		=> ctx.Items.TryGetValue(ItemKey, out var v) && v is BodyLogPolicy p
			? p
			: BodyLogPolicy.Default;
}