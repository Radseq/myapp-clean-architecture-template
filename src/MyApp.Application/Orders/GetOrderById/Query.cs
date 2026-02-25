using MyApp.Application.Common.Caching;
using MyApp.Application.Common.Messaging;

namespace MyApp.Application.Orders.GetOrderById;

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