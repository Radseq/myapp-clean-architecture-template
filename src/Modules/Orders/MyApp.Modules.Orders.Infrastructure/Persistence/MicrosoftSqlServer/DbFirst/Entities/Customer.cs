using System;
using System.Collections.Generic;

namespace MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

public partial class Customer
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
