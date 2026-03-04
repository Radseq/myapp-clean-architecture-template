using MyApp.Modules.Orders.Application.Features.GetOrderById;

namespace MyApp.Modules.Orders.Application.Abstractions.Persistence;

public interface IOrderReadRepository
{
	Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct);
}
