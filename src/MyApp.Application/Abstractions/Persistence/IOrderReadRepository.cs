using MyApp.Application.Orders.GetOrderById;

namespace MyApp.Application.Abstractions.Persistence;

public interface IOrderReadRepository
{
	Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct);
}
