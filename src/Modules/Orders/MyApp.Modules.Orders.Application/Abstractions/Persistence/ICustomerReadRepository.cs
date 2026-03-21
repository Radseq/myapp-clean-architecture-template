namespace MyApp.Modules.Orders.Application.Abstractions.Persistence;

public interface ICustomerReadRepository
{
	Task<bool> ExistsAsync(int customerId, CancellationToken ct);
}
