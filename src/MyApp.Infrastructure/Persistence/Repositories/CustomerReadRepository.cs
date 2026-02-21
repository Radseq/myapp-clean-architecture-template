using Microsoft.EntityFrameworkCore;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;

namespace MyApp.Infrastructure.Persistence.Repositories;

public sealed class CustomerReadRepository(AppDbContext db) : ICustomerReadRepository
{
    public Task<bool> ExistsAsync(int customerId, CancellationToken ct)
        => db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, ct);
}
