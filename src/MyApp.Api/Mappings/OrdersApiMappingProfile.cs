using AutoMapper;
using MyApp.Application.Orders.Commands.CreateOrder;
using MyApp.Application.Orders.Dtos;
using MyApp.Shared;

namespace MyApp.Api.Mappings;
public sealed class OrdersApiMappingProfile : Profile
{
	public OrdersApiMappingProfile()
	{
		CreateMap<CreateOrderAndDispatchTransportRequestDto, CreateOrderAndDispatchTransportCommand>()
			// OrderDateUtc: DTO może być DateTimeOffset? -> command DateTime?
			.ForCtorParam(nameof(CreateOrderAndDispatchTransportCommand.OrderDateUtc),
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

        CreateMap<CreateOrderAndDispatchTransportRequestDto, CreateOrderCommand>()
		// OrderDateUtc: DTO może być DateTimeOffset? -> command DateTime?
		.ForCtorParam(nameof(CreateOrderCommand.OrderDateUtc),
			opt => opt.MapFrom(src => src.OrderDateUtc.HasValue
				? src.OrderDateUtc.Value.UtcDateTime
				: (DateTime?)null));
    }
}
