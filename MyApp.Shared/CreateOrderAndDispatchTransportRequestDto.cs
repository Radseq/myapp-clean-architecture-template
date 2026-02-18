namespace MyApp.Shared;

public sealed record CreateOrderAndDispatchTransportRequestDto(
	int CustomerId,
	DateTimeOffset? OrderDateUtc,
	IReadOnlyList<CreateOrderItemDto> Items
);

public sealed record CreateOrderItemDto(
	int ProductId,
	decimal UnitPrice,
	int Quantity
);

//public sealed record CreateOrderRequestDto(
//    int CustomerId,
//    DateTimeOffset? OrderDateUtc,
//    IReadOnlyList<CreateOrderItemDto> Items
//);