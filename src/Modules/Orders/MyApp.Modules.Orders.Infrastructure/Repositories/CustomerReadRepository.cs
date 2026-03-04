using Microsoft.EntityFrameworkCore;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;

namespace MyApp.Modules.Orders.Infrastructure.Repositories;

public sealed class CustomerReadRepository(OrdersDbContext db) : ICustomerReadRepository
{
    public Task<bool> ExistsAsync(int customerId, CancellationToken ct)
        => db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct);
}
