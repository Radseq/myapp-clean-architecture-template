namespace MyApp.Application.Orders.Dtos;

public sealed record OrderDto(
    int Id,
    int CustomerId,
    DateTime OrderDateUtc,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    int ProductId,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
