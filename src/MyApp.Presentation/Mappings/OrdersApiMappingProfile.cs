using AutoMapper;
using MyApp.Application.Orders.CreateOrder;
using MyApp.Application.Orders.CreateOrderAndDispatchTransport;
using MyApp.Application.Orders.GetOrderById;
using MyApp.Contracts;

namespace MyApp.Presentation.Mappings;
public sealed class OrdersApiMappingProfile : Profile
{
	public OrdersApiMappingProfile()
	{
        CreateMap<CreateOrderAndDispatchTransportRequestDto, Application.Orders.CreateOrderAndDispatchTransport.Command>()
			// OrderDateUtc: DTO może być DateTimeOffset? -> command DateTime?
			.ForCtorParam(nameof(Application.Orders.CreateOrderAndDispatchTransport.Command.OrderDateUtc),
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

        CreateMap<CreateOrderAndDispatchTransportRequestDto, Application.Orders.CreateOrder.Command>()
		// OrderDateUtc: DTO może być DateTimeOffset? -> command DateTime?
		.ForCtorParam(nameof(Application.Orders.CreateOrder.Command.OrderDateUtc),
			opt => opt.MapFrom(src => src.OrderDateUtc.HasValue
				? src.OrderDateUtc.Value.UtcDateTime
				: (DateTime?)null));
    }
}
