using MyApp.Domain.Common;

namespace MyApp.Domain.Orders;

public sealed class OrderItem
{
    public int ProductId { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    private OrderItem(int productId, decimal unitPrice, int quantity)
    {
        ProductId = productId;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public static MessageResult<OrderItem> Create(int productId, decimal unitPrice, int quantity)
    {
        if (productId <= 0)
            return MessageResult<OrderItem>.Fail(OrderErrors.ProductIdInvalid.WithArgs(productId));

        if (unitPrice <= 0m)
            return MessageResult<OrderItem>.Fail(OrderErrors.UnitPriceInvalid.WithArgs(unitPrice));

        if (quantity <= 0)
            return MessageResult<OrderItem>.Fail(OrderErrors.QuantityInvalid.WithArgs(quantity));

        return MessageResult<OrderItem>.Ok(new OrderItem(productId, unitPrice, quantity));
    }

    public MessageResult IncreaseQuantity(int delta)
    {
        if (delta <= 0)
            return MessageResult.Fail(OrderErrors.DeltaInvalid.WithArgs(delta));

        Quantity += delta;
        return MessageResult.Ok();
    }

    public MessageResult DecreaseQuantity(int delta)
    {
        if (delta <= 0)
            return MessageResult.Fail(OrderErrors.DeltaInvalid.WithArgs(delta));

        if (Quantity - delta <= 0)
            return MessageResult.Fail(OrderErrors.QuantityTooLow.WithArgs(Quantity, delta));

        Quantity -= delta;
        return MessageResult.Ok();
    }
}
