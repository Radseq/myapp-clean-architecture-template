using MyApp.BuildingBlocks.Application.Common.Messaging;
using MyApp.Modules.Orders.Application.Features.CreateOrderAndDispatchTransport;

namespace MyApp.Modules.Orders.Application.Features.CreateOrder;

public sealed record Command(
	int CustomerId,
	DateTime? OrderDateUtc,
	List<CreateOrderItemRequest> Items
) : ICommand<CreateOrderAndDispatchTransportResponse>, ISkipUnitOfWorkBehavior;

// ISkipUnitOfWorkBehavior or ITransactionalCommand etc\
// ISkipUnitOfWorkBehavior by nie robi³ automatycznie begin async, save async, commit, rollback