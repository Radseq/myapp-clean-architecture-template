using MyApp.Application.Common.Messaging;
using MyApp.Application.Orders.CreateOrderAndDispatchTransport;

namespace MyApp.Application.Orders.CreateOrder;

public sealed record Command(
	int CustomerId,
	DateTime? OrderDateUtc,
	List<CreateOrderItemRequest> Items
) : ICommand, ITransactionalCommand;

// ISkipUnitOfWorkBehavior or ITransactionalCommand etc\
// ISkipUnitOfWorkBehavior by nie robi³ automatycznie begin async, save async, commit, rollback