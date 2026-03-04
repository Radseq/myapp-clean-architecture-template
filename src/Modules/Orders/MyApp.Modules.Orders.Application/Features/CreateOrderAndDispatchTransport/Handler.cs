using MediatR;
using MyApp.BuildingBlocks.Application.Abstractions.Outbox;
using MyApp.BuildingBlocks.Domain.Common;
using MyApp.IntegrationContracts.Outbox;
using MyApp.IntegrationContracts.Transport.Commands;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Domain.Orders;
using System.Text.Json;

namespace MyApp.Modules.Orders.Application.Features.CreateOrderAndDispatchTransport;

public sealed class Handler(
	IOrdersUnitOfWork uow,
	ICustomerReadRepository customers,
	IOrderDomainRepository orders,
	IOutboxWriter<OutboxOwners.Orders> outbox,
	IOutboxDispatcher<OutboxOwners.Orders> outboxDispatcher) 
	: IRequestHandler<Command, MessageResult<CreateOrderAndDispatchTransportResponse>>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public async Task<MessageResult<CreateOrderAndDispatchTransportResponse>> Handle(
		Command request,
		CancellationToken cancellationToken)
	{
		if (request.Items is null || request.Items.Count == 0)
			return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(Errors.Orders.EmptyItems);

		if (!await customers.ExistsAsync(request.CustomerId, cancellationToken))
			return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(Errors.Customers.NotFound(request.CustomerId));

		var orderDateUtc = request.OrderDateUtc ?? DateTime.UtcNow;

		// wa¿ne: generuj OUTSIDE tx (retry mo¿e powtórzyæ delegate)
		var transportCorrelationId = $"order-{Guid.NewGuid():N}";

		// 1) TX: Order + Outbox w jednej transakcji (retry-safe)
		var tx = await uow.ExecuteInTransactionAsync(async c =>
		{
			// buduj obiekty w œrodku delegate (bezpieczniej przy retry)
			var items = new List<OrderItem>(request.Items.Count);
			foreach (var r in request.Items)
			{
				var it = OrderItem.Create(r.ProductId, r.UnitPrice, r.Quantity);
				if (!it.IsSuccess)
					return MessageResult<TxResult>.Fail(it.PrimaryError!);

				items.Add(it.Value!);
			}

			var orderRes = Order.Create(request.CustomerId, orderDateUtc, items);
			if (!orderRes.IsSuccess)
				return MessageResult<TxResult>.Fail(orderRes.PrimaryError!);

			var order = orderRes.Value!;

			var confirm = order.Confirm();
			if (!confirm.IsSuccess)
				return MessageResult<TxResult>.Fail(confirm.PrimaryError!);

			var add = await orders.AddAsync(order, c);
			if (add.HasFailed)
				return MessageResult<TxResult>.Fail(add.Errors);

			// Save #1: ¿eby dostaæ order.Id (identity)
			var save1 = await uow.SaveChangesAsync(c);
			if (save1.HasFailed)
				return MessageResult<TxResult>.Fail(save1.Errors);

			var dto = new CreateTransportOrderV1(
				ExternalCorrelationId: transportCorrelationId,
				OrderId: order.Id,
				CustomerId: order.CustomerId,
				OrderDateUtc: order.OrderDateUtc,
				TotalAmount: order.TotalAmount,
				Items: order.Items
					.Select(i => new CreateTransportOrderItemV1(i.ProductId, i.Quantity, i.UnitPrice))
					.ToList());


			var payloadJson = JsonSerializer.Serialize(dto, JsonOptions);

			// Outbox insert (Pending)
			var outboxId = outbox.EnqueuePending(
				type: TransportOutboxTypes.TransportOrderCreatedV1,
				idempotencyKey: transportCorrelationId, // to samo co idempotency header downstream
				payloadJson: payloadJson);

			// Save #2: utrwalamy outbox
			var save2 = await uow.SaveChangesAsync(c);
			if (save2.HasFailed)
				return MessageResult<TxResult>.Fail(save2.Errors);

			return MessageResult<TxResult>.Ok(new TxResult(order.Id, outboxId));
		}, cancellationToken);

		if (tx.HasFailed)
			return MessageResult<CreateOrderAndDispatchTransportResponse>.Fail(tx.Errors);

		// 2) PO COMMITCIE: best-effort "hot dispatch" (opcjonalne)
		var sentNow = await outboxDispatcher.TryDispatchOnceAsync(tx.Value!.OutboxId, cancellationToken);

		var response = new CreateOrderAndDispatchTransportResponse
		{
			OrderId = tx.Value.OrderId,
			// REKOMENDACJA: zmieñ typ/kontrakt na Guid (to jest prawdziwy identyfikator integracji)
			TransportOrderId = tx.Value.OutboxId,
			TransportStatus = sentNow ? "Sent" : "Queued",
			TransportCorrelationId = transportCorrelationId
		};

		// przy outbox: stworzenie zamówienia jest SUCCESS, a wysy³ka jest eventual consistency
		return MessageResult<CreateOrderAndDispatchTransportResponse>.Ok(response);
	}

	private sealed record TxResult(int OrderId, Guid OutboxId);
}
