using MyApp.BuildingBlocks.Application.Abstractions.Outbox;
using MyApp.IntegrationContracts.Outbox;
using MyApp.IntegrationContracts.Transport.Commands;
using MyApp.Modules.Transport.Application.Abstractions;
using System.Text.Json;

public sealed class TransportOrderCreatedOutboxHandler(ITransportApiClient transportApi)
	: IOutboxMessageHandler<OutboxOwners.Orders>
{
	public string Type => TransportOutboxTypes.TransportOrderCreatedV1;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public async Task<OutboxDispatchResult> DispatchAsync(OutboxEnvelope envelope, CancellationToken ct)
	{
		CreateTransportOrderV1 dto;
		try
		{
			dto = JsonSerializer.Deserialize<CreateTransportOrderV1>(envelope.PayloadJson, JsonOptions)
				  ?? throw new JsonException("Null payload.");
		}
		catch (Exception ex)
		{
			return OutboxDispatchResult.Dead($"Invalid payload: {ex.Message}");
		}

		var send = await transportApi.SendTransportOrderAsync(dto, ct);

		return send.IsSuccess
			? OutboxDispatchResult.Done()
			: OutboxDispatchResult.Retry(send.PrimaryError?.Key ?? "Transport send failed");
	}
}