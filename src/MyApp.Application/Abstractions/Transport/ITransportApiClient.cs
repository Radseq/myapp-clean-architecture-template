using MyApp.Domain.Common;

namespace MyApp.Application.Abstractions.Transport;

public interface ITransportApiClient
{
    Task<MessageResult> SendTransportOrderAsync(TransportOrderExternalDto dto, CancellationToken ct);
}

public sealed record TransportOrderExternalDto(
    string ExternalCorrelationId,
    int OrderId,
    int CustomerId,
    DateTime OrderDateUtc,
    decimal TotalAmount,
    IReadOnlyList<TransportOrderExternalItemDto> Items
);

public sealed record TransportOrderExternalItemDto(
    int ProductId,
    int Quantity,
    decimal UnitPrice
);
