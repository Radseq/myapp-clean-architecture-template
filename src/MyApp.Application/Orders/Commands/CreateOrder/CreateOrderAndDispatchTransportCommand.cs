using MyApp.Application.Common.Messaging;

namespace MyApp.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderAndDispatchTransportCommand(
	int CustomerId,
	DateTime? OrderDateUtc,
	List<CreateOrderItemRequest> Items
) : ICommand<CreateOrderAndDispatchTransportResponse>, ISkipUnitOfWorkBehavior;

// ISkipUnitOfWorkBehavior or ITransactionalCommand etc\
// ISkipUnitOfWorkBehavior by nie robi³ automatycznie begin async, save async, commit, rollback

public sealed record CreateOrderItemRequest(
    int ProductId,
    decimal UnitPrice,
    int Quantity);

public sealed class CreateOrderAndDispatchTransportResponse
{
    public int OrderId { get; set; }
    public Guid TransportOrderId { get; set; }
    public string TransportStatus { get; set; } = "";
    public string TransportCorrelationId { get; set; } = "";
}
