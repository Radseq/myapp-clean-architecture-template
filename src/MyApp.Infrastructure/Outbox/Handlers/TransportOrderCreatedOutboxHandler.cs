using System.Text.Json;
using MyApp.Application.Abstractions.Outbox;
using MyApp.Application.Abstractions.Transport;

namespace MyApp.Infrastructure.Outbox.Handlers;

public sealed class TransportOrderCreatedOutboxHandler(ITransportApiClient transportApi)
    : IOutboxMessageHandler
{
    public string Type => "TransportOrderCreated"; // musi pasować do type w outbox.EnqueuePending(...)

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<OutboxDispatchResult> DispatchAsync(OutboxEnvelope envelope, CancellationToken ct)
    {
        TransportOrderExternalDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<TransportOrderExternalDto>(envelope.PayloadJson, JsonOptions)
                  ?? throw new JsonException("Null payload.");
        }
        catch (Exception ex)
        {
            // payload niepoprawny -> nigdy nie zadziała na retry
            return OutboxDispatchResult.Dead($"Invalid payload: {ex.Message}");
        }

        var send = await transportApi.SendTransportOrderAsync(dto, ct);

        if (send.IsSuccess)
            return OutboxDispatchResult.Done();

        // Na start: Retry dla fail (worker i tak ma MaxAttempts + backoff).
        // Potem możesz tu doprecyzować: 4xx=>Dead, 409=>Done itd.
        return OutboxDispatchResult.Retry(send.PrimaryError?.Key ?? "Transport send failed");
    }
}
