using Microsoft.EntityFrameworkCore;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Domain.Common;
using MyApp.Domain.Orders;
using MyApp.Infrastructure.Persistence.DbFirst;

namespace MyApp.Infrastructure.Persistence.Repositories;

public sealed class OrderDomainRepository(AppDbContext db,
    IUnitOfWork uow) : IOrderDomainRepository
{
    public Task<MessageResult> AddAsync(Order order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        var efOrder = MapToEf(order);
        db.Orders.Add(efOrder);

        uow.EnqueuePostSave(() => order.SetPersistedId(efOrder.Id)); // ma być „niezawodne” i proste

        return Task.FromResult(MessageResult.Ok());
    }

    public async Task<Order?> GetDomainByIdAsync(int orderId, CancellationToken ct)
    {
        var ef = await db.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (ef is null) return null;

        var items = new List<OrderItem>(ef.OrderItems.Count);
        foreach (var it in ef.OrderItems)
        {
            var created = OrderItem.Create(it.ProductId, it.UnitPrice, it.Quantity);
            if (!created.IsSuccess) return null;
            items.Add(created.Value!);
        }

        _ = Enum.TryParse<OrderStatus>(ef.Status, ignoreCase: true, out var status);
        if (!Enum.IsDefined(typeof(OrderStatus), status))
            status = OrderStatus.Draft;

        var rehydrated = Order.Rehydrate(
            id: ef.Id,
            customerId: ef.CustomerId,
            orderDateUtc: ef.OrderDateUtc,
            status: status,
            items: items);

        return rehydrated.IsSuccess ? rehydrated.Value : null;
    }

    private static DbFirst.Entities.Order MapToEf(Order order)
        => new()
        {
            CustomerId = order.CustomerId,
            OrderDateUtc = order.OrderDateUtc,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            OrderItems = order.Items.Select(i => new DbFirst.Entities.OrderItem
            {
                ProductId = i.ProductId,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };
}
