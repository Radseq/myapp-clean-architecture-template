using MediatR;
using MyApp.BuildingBlocks.Domain.Common;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Domain.Orders;

namespace MyApp.Modules.Orders.Application.Features.CreateOrder;

public sealed class Handler(
	ICustomerReadRepository customers,
	IOrderDomainRepository orders)
		: IRequestHandler<Command, MessageResult>
{
	public async Task<MessageResult> Handle(
		Command request, CancellationToken cancellationToken)
	{
		if (request.Items is null || request.Items.Count == 0)
			return MessageResult.Fail(Errors.Orders.EmptyItems);

		if (!await customers.ExistsAsync(request.CustomerId, cancellationToken))
			return MessageResult.Fail(Errors.Customers.NotFound(request.CustomerId));

		var items = new List<OrderItem>();
		foreach (var r in request.Items)
		{
			var it = OrderItem.Create(r.ProductId, r.UnitPrice, r.Quantity);
			if (!it.IsSuccess)
				return MessageResult.Fail(it.PrimaryError!);

			items.Add(it.Value!);
		}

		var orderRes = Order.Create(request.CustomerId, request.OrderDateUtc ?? DateTime.UtcNow, items);
		if (!orderRes.IsSuccess)
			return MessageResult.Fail(orderRes.PrimaryError!);

		var order = orderRes.Value!;

		var confirm = order.Confirm();
		if (!confirm.IsSuccess)
			return MessageResult.Fail(confirm.PrimaryError!);

		var saved = await orders.AddAsync(order, cancellationToken);
		if (saved.HasFailed)
		{
			return MessageResult.Fail(saved.Errors);
		}

		return MessageResult.Ok();
	}
}
