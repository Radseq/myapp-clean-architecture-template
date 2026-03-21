using AutoMapper;
using MyApp.Modules.Orders.Application.Features.CreateOrderAndDispatchTransport;
using MyApp.Modules.Orders.Application.Features.GetOrderById;
using MyApp.Modules.Orders.Contracts;

namespace MyApp.Modules.Orders.Presentation.Mappings;
public sealed class OrdersApiMappingProfile : Profile
{
	public OrdersApiMappingProfile()
	{
		CreateMap<CreateOrderAndDispatchTransportRequestDto, Command>()
			// OrderDateUtc: DTO może być DateTimeOffset? -> command DateTime?
			.ForCtorParam(nameof(Command.OrderDateUtc),
				opt => opt.MapFrom(src => src.OrderDateUtc.HasValue
					? src.OrderDateUtc.Value.UtcDateTime
					: (DateTime?)null));

		CreateMap<CreateOrderItemDto, CreateOrderItemRequest>();

		CreateMap<CreateOrderAndDispatchTransportResponse, CreateOrderAndDispatchTransportResponseDto>();

		CreateMap<OrderItemDto, OrderItemResponseModel>();

		CreateMap<OrderDto, OrderResponseModel>()
			// Items will map automatically because the element map exists
			.ForCtorParam(nameof(OrderResponseModel.Items),
				opt => opt.MapFrom(s => s.Items));

		CreateMap<CreateOrderAndDispatchTransportRequestDto, Application.Features.CreateOrder.Command>()
		// OrderDateUtc: DTO może być DateTimeOffset? -> command DateTime?
		.ForCtorParam(nameof(Application.Features.CreateOrder.Command.OrderDateUtc),
			opt => opt.MapFrom(src => src.OrderDateUtc.HasValue
				? src.OrderDateUtc.Value.UtcDateTime
				: (DateTime?)null));
	}
}
