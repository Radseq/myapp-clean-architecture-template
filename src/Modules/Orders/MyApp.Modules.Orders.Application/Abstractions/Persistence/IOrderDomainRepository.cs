using MyApp.BuildingBlocks.Domain.Common;
using MyApp.Modules.Orders.Domain.Orders;

namespace MyApp.Modules.Orders.Application.Abstractions.Persistence;

public interface IOrderDomainRepository
{
    Task<MessageResult> AddAsync(Order order, CancellationToken ct);
    Task<Order?> GetDomainByIdAsync(int orderId, CancellationToken ct);
}
