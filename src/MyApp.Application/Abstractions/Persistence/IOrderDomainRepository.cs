using MyApp.Domain.Common;
using MyApp.Domain.Orders;

namespace MyApp.Application.Abstractions.Persistence;

public interface IOrderDomainRepository
{
    Task<MessageResult> AddAsync(Order order, CancellationToken ct);
    Task<Order?> GetDomainByIdAsync(int orderId, CancellationToken ct);
}
