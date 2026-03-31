using AutoMapper;
using MyApp.Modules.Orders.Application.Features.GetOrderById;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

namespace MyApp.Modules.Orders.Infrastructure.Mapping;

public sealed class OrdersReadProfile : Profile
{
	public OrdersReadProfile()
	{
		CreateMap<OrderItem, OrderItemDto>()
			// record ctor param: LineTotal
			.ForCtorParam(nameof(OrderItemDto.LineTotal),
				opt => opt.MapFrom(src => src.UnitPrice * src.Quantity));

		CreateMap<Order, OrderDto>()
			// record ctor param: Items
			.ForCtorParam(nameof(OrderDto.Items),
				opt => opt.MapFrom(src => src.OrderItems));
	}
}
