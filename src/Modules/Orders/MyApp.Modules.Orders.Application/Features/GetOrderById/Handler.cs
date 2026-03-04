using MediatR;
using MyApp.BuildingBlocks.Domain.Common;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;

namespace MyApp.Modules.Orders.Application.Features.GetOrderById;

public sealed class Handler(IOrderDomainRepository ordersDomian,
	IOrderReadRepository ordersDto) : 
	IRequestHandler<Query, MessageResult<OrderDto>>
{
	public async Task<MessageResult<OrderDto>> Handle(Query request, CancellationToken ct)
	{
		// wersja 1, Persistence zwaca order z domian 
		// najlepsza bo pomimo tego że są zweryfikowane przez domian dane na bazie,
		// ale:
		// ktoś zedytował ręcznie, 
		// dane są stare
		var order = await ordersDomian.GetDomainByIdAsync(request.Id, ct);
		if (order is null)
			return MessageResult<OrderDto>.Fail(Errors.Orders.NotFound(request.Id));

		var dto = new OrderDto(
			Id: order.Id,
			CustomerId: order.CustomerId,
			OrderDateUtc: order.OrderDateUtc,
			Status: order.Status.ToString(),
			TotalAmount: order.TotalAmount,
			Items: order.Items.Select(i => new OrderItemDto(i.ProductId, i.UnitPrice, i.Quantity, i.LineTotal)).ToList()
		);

		// wersja 2, Persistence zwraca dto, wersję 2 można wykożystać tylko dla get,
		// tu w Persistence jest automapper który mappuje dane z bazy do dto
		var orderDto = await ordersDto.GetByIdAsync(request.Id, ct);

		return MessageResult<OrderDto>.Ok(dto);
	}
}
