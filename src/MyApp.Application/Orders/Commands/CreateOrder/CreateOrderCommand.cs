using MyApp.Application.Common.Messaging;

namespace MyApp.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
	int CustomerId,
	DateTime? OrderDateUtc,
	List<CreateOrderItemRequest> Items
) : ICommand, ITransactionalCommand;

// ISkipUnitOfWorkBehavior or ITransactionalCommand etc\
// ISkipUnitOfWorkBehavior by nie robi³ automatycznie begin async, save async, commit, rollback