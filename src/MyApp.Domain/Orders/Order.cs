using MyApp.Domain.Common;

namespace MyApp.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = new();

    public int Id { get; private set; }
    public int CustomerId { get; private set; }
    public DateTime OrderDateUtc { get; private set; }
    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;
    public decimal TotalAmount => _items.Sum(i => i.LineTotal);

    private Order(int customerId, DateTime orderDateUtc)
    {
        CustomerId = customerId;
        OrderDateUtc = orderDateUtc;
        Status = OrderStatus.Draft;
    }

    public static MessageResult<Order> Create(int customerId, DateTime orderDateUtc, IEnumerable<OrderItem> items)
    {
        if (customerId <= 0)
            return MessageResult<Order>.Fail(OrderErrors.CustomerInvalid.WithArgs(customerId));

        var list = items?.ToList() ?? new List<OrderItem>();
        if (list.Count == 0)
            return MessageResult<Order>.Fail(OrderErrors.ItemsEmpty);

        var order = new Order(customerId, orderDateUtc);

        foreach (var it in list)
        {
            // items wchodz¹ ju¿ jako domenowe (zak³adamy: przesz³y Create)
            // merge rule trzymamy w jednym miejscu
            var add = order.AddItem(it);
            if (!add.IsSuccess)
                return MessageResult<Order>.Fail(add.PrimaryError!);
        }

        var rules = order.ValidateBusinessRules();
        if (!rules.IsSuccess)
            return MessageResult<Order>.Fail(rules.PrimaryError!);

        return MessageResult<Order>.Ok(order);
    }

    /// <summary>
    /// Rehydrate from persistence without enforcing status transitions.
    /// Used by repositories. Still validates basic invariants.
    /// </summary>
    public static MessageResult<Order> Rehydrate(
        int id,
        int customerId,
        DateTime orderDateUtc,
        OrderStatus status,
        IEnumerable<OrderItem> items)
    {
        if (id <= 0)
            return MessageResult<Order>.Fail(OrderErrors.IdInvalid.WithArgs(id));

        if (customerId <= 0)
            return MessageResult<Order>.Fail(OrderErrors.CustomerInvalid.WithArgs(customerId));

        var list = items?.ToList() ?? new List<OrderItem>();
        if (list.Count == 0)
            return MessageResult<Order>.Fail(OrderErrors.ItemsEmpty);

        var order = new Order(customerId, orderDateUtc);

        // celowo bez merge: odzwierciedlamy persisted data 1:1
        order._items.AddRange(list);

        order.Id = id;
        order.Status = status;

        var rules = order.ValidateBusinessRules();
        if (!rules.IsSuccess)
            return MessageResult<Order>.Fail(rules.PrimaryError!);

        return MessageResult<Order>.Ok(order);
    }

    public MessageResult AddItem(int productId, decimal unitPrice, int quantity)
    {
        var created = OrderItem.Create(productId, unitPrice, quantity);
        if (!created.IsSuccess)
            return MessageResult.Fail(created.PrimaryError!);

        return AddItem(created.Value!);
    }

    public MessageResult AddItem(OrderItem item)
    {
        if (Status != OrderStatus.Draft)
            return MessageResult.Fail(
                OrderErrors.ModifyNotAllowed.WithArgs(Status.ToString()));

        // merge: ProductId + UnitPrice
        var existing = _items.FirstOrDefault(x => x.ProductId == item.ProductId && x.UnitPrice == item.UnitPrice);
        if (existing is null)
        {
            _items.Add(item);
            return MessageResult.Ok();
        }

        // zwiêkszamy iloœæ o item.Quantity (a nie “quantity” z zewn¹trz)
        return existing.IncreaseQuantity(item.Quantity);
    }

    public MessageResult RemoveItem(int productId)
    {
        if (Status != OrderStatus.Draft)
            return MessageResult.Fail(
                OrderErrors.ModifyNotAllowed.WithArgs(Status.ToString()));

        var idx = _items.FindIndex(x => x.ProductId == productId);
        if (idx < 0)
            return MessageResult.Fail(
                OrderErrors.ItemNotFound.WithArgs(productId));

        _items.RemoveAt(idx);

        if (_items.Count == 0)
            return MessageResult.Fail(OrderErrors.ItemsEmpty);

        return MessageResult.Ok();
    }

    public MessageResult Confirm()
    {
        if (Status != OrderStatus.Draft)
            return MessageResult.Fail(
                OrderErrors.ConfirmInvalidState.WithArgs(Status.ToString()));

        var rules = ValidateBusinessRules();
        if (!rules.IsSuccess)
            return rules;

        Status = OrderStatus.Confirmed;
        return MessageResult.Ok();
    }

    public MessageResult Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            return MessageResult.Ok();

        // przyk³adowa polityka: confirmed nie mo¿na anulowaæ
        if (Status == OrderStatus.Confirmed)
            return MessageResult.Fail(OrderErrors.CancelNotAllowed);

        Status = OrderStatus.Cancelled;
        return MessageResult.Ok();
    }

    private MessageResult ValidateBusinessRules()
    {
        // regu³a biznesowa: limit
        if (_items.Any(i => i.Quantity > 1000))
            return MessageResult.Fail(OrderErrors.QuantityTooHigh.WithArgs(1000));

        if (TotalAmount <= 0m)
            return MessageResult.Fail(OrderErrors.TotalInvalid.WithArgs(TotalAmount));

        return MessageResult.Ok();
    }

    public void SetPersistedId(int id) => Id = id;
}
