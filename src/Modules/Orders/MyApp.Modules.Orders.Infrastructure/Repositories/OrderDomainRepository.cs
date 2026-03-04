using Microsoft.EntityFrameworkCore;
using MyApp.BuildingBlocks.Domain.Common;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Domain.Orders;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;

namespace MyApp.Modules.Orders.Infrastructure.Repositories;

public sealed class OrderDomainRepository(OrdersDbContext db,
	IOrdersUnitOfWork uow) : IOrderDomainRepository
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

    private static Persistence.MicrosoftSqlServer.DbFirst.Entities.Order MapToEf(Order order)
        => new()
        {
            CustomerId = order.CustomerId,
            OrderDateUtc = order.OrderDateUtc,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            OrderItems = order.Items.Select(i => new Persistence.MicrosoftSqlServer.DbFirst.Entities.OrderItem
            {
                ProductId = i.ProductId,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };
}
