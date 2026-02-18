namespace MyApp.Shared;

public sealed record CreateOrderAndDispatchTransportResponseDto(
    int OrderId,
    Guid TransportOrderId,
    string TransportStatus,
    string CorrelationId
);
