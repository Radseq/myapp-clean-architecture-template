using MyApp.BuildingBlocks.Application.Common.Caching;
using MyApp.BuildingBlocks.Application.Common.Messaging;

namespace MyApp.Modules.Orders.Application.Features.GetOrderById;

public sealed record Query(int Id)
	: ICacheableQuery<OrderDto>
{
	public string CacheKey => $"orders:id:{Id}";
	public TimeSpan Ttl => TimeSpan.FromSeconds(10);

	public bool CacheNotFound => true;
	public TimeSpan NotFoundTtl => TimeSpan.FromMilliseconds(10000);

	public CacheScope Scope => CacheScope.Global;

	public bool VaryByRoles => false;
}