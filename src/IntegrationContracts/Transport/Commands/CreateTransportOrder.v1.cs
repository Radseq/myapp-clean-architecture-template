namespace MyApp.IntegrationContracts.Transport.Commands;

public static class TransportOutboxTypes
{
    public const string TransportOrderCreatedV1 = "TransportOrderCreated.v1";
}

public sealed record CreateTransportOrderV1(
    string ExternalCorrelationId,
    int OrderId,
    int CustomerId,
    DateTime OrderDateUtc,
    decimal TotalAmount,
    IReadOnlyList<CreateTransportOrderItemV1> Items);

public sealed record CreateTransportOrderItemV1(
    int ProductId,
    int Quantity,
    decimal UnitPrice);