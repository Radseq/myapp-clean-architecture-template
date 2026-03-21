namespace MyApp.Modules.Orders.Contracts;

public sealed record CreateOrderAndDispatchTransportResponseDto(
	int OrderId,
	Guid TransportOrderId,
	string TransportStatus,
	string TransportCorrelationId
);
