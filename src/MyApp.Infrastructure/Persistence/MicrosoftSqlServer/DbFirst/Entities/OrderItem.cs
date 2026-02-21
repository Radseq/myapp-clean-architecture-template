using System;
using System.Collections.Generic;

namespace MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

public partial class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public virtual Order Order { get; set; } = null!;
}
