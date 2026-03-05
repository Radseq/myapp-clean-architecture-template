using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.IntegrationContracts.Outbox;
using MyApp.BuildingBlocks.Application.Abstractions.Outbox;
using MyApp.Modules.Transport.Application.Abstractions;
using MyApp.Modules.Transport.Infrastructure.ExternalServices;
using MyApp.BuildingBlocks.Infrastructure.Http;

namespace MyApp.Modules.Transport.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddTransportInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddHttpClient<ITransportApiClient, TransportApiClient>(http =>
		{
			http.BaseAddress = new Uri(configuration["TransportApi:BaseUrl"]
				?? throw new InvalidOperationException("Missing TransportApi:BaseUrl"));
		}).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

		// Handler, który konsumuje OUTBOX Orders (typed marker z IntegrationContracts!)
		services.AddScoped<IOutboxMessageHandler<OutboxOwners.Orders>, TransportOrderCreatedOutboxHandler>();

		return services;
	}
}