using Microsoft.Extensions.Logging;
using MyApp.BuildingBlocks.Infrastructure.Persistence;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;

namespace MyApp.Modules.Orders.Infrastructure.Persistence;

public sealed class OrdersUnitOfWork : EfUnitOfWork<OrdersDbContext>, IOrdersUnitOfWork
{
	public OrdersUnitOfWork(OrdersDbContext db, ILogger<OrdersUnitOfWork> logger)
		: base(db, logger) { }
}