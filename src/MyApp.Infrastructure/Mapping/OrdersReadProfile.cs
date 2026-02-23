using AutoMapper;
using MyApp.Application.Orders.Dtos;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

namespace MyApp.Infrastructure.Mapping;

public sealed class OrdersReadProfile : Profile
{
	public OrdersReadProfile()
	{
		CreateMap<OrderItem, OrderItemDto>()
			// record ctor param: LineTotal
			.ForCtorParam(nameof(OrderItemDto.LineTotal),
				opt => opt.MapFrom(src => src.UnitPrice * src.Quantity));

		CreateMap<Order, OrderDto>()
			// record ctor param: Items (u Ciebie encja ma OrderItems)
			.ForCtorParam(nameof(OrderDto.Items),
				opt => opt.MapFrom(src => src.OrderItems));
	}
}
