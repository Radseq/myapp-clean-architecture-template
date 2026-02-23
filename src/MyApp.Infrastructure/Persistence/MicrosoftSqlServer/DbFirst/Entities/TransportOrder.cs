using System;
using System.Collections.Generic;

namespace MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;

public partial class TransportOrder
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string ExternalCorrelationId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string? FailedReason { get; set; }

    public virtual Order Order { get; set; } = null!;
}
