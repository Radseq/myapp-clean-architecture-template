namespace MyApp.Application.Abstractions.Outbox;

/// <summary>
/// Dodaje wiadomość do outbox w tej samej transakcji co zmiany domenowe.
/// Nie wywołuje SaveChanges (robi to UoW/pipeline).
/// </summary>
public interface IOutboxWriter
{
    Guid EnqueuePending(string type, string idempotencyKey, string payloadJson);
}
