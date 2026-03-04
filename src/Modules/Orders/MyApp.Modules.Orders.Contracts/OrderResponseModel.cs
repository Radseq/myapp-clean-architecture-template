namespace MyApp.Modules.Orders.Contracts;

public sealed record OrderResponseModel(
	int Id,
	int CustomerId,
	DateTime OrderDateUtc,
	string Status,
	decimal TotalAmount,
	IReadOnlyList<OrderItemResponseModel> Items);

public sealed record OrderItemResponseModel(
	int ProductId,
	decimal UnitPrice,
	int Quantity,
	decimal LineTotal);
