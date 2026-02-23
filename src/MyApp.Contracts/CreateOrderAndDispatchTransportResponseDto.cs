namespace MyApp.Contracts;

public sealed record CreateOrderAndDispatchTransportResponseDto(
    int OrderId,
    Guid TransportOrderId,
    string TransportStatus,
    string TransportCorrelationId
);
