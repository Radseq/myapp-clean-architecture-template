using MediatR;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Application.Common;
using MyApp.Domain.Common;
using MyApp.Domain.Orders;

namespace MyApp.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderHandler(
    ICustomerReadRepository customers,
    IOrderDomainRepository orders)
        : IRequestHandler<CreateOrderCommand, MessageResult>
{
    public async Task<MessageResult> Handle(
        CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(Errors.Orders.EmptyItems);

        if (!await customers.ExistsAsync(request.CustomerId, cancellationToken))
            return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(Errors.Customers.NotFound(request.CustomerId));

        var items = new List<OrderItem>();
        foreach (var r in request.Items)
        {
            var it = OrderItem.Create(r.ProductId, r.UnitPrice, r.Quantity);
            if (!it.IsSuccess)
                return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(it.PrimaryError!);

            items.Add(it.Value!);
        }

        var orderRes = Order.Create(request.CustomerId, request.OrderDateUtc ?? DateTime.UtcNow, items);
        if (!orderRes.IsSuccess)
            return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(orderRes.PrimaryError!);

        var order = orderRes.Value!;

        var confirm = order.Confirm();
        if (!confirm.IsSuccess)
            return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(confirm.PrimaryError!);

        var saved = await orders.AddAsync(order, cancellationToken);
        if (saved.HasFailed)
        {
            return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(saved.Errors);
        }

        return MessageResult<CreateOrderAndDispatchTransportResponse>.Ok();
    }
}
