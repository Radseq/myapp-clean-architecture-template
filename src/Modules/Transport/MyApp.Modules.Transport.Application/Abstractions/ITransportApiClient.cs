using MyApp.BuildingBlocks.Domain.Common;
using MyApp.IntegrationContracts.Transport.Commands;

namespace MyApp.Modules.Transport.Application.Abstractions;

public interface ITransportApiClient
{
    Task<MessageResult> SendTransportOrderAsync(CreateTransportOrderV1 dto, CancellationToken ct);
}