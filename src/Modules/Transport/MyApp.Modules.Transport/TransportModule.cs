using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Modules.Transport.Application;
using MyApp.Modules.Transport.Infrastructure;

namespace MyApp.Modules.Transport;

public static class TransportModule
{
	public static IServiceCollection AddTransportModule(
		this IServiceCollection services,
		IConfiguration configuration,
		IMvcBuilder mvc)
	{
		services.AddTransportApplication();
		services.AddTransportInfrastructure(configuration);
		// mvc.AddTransportPresentation(); // jak dodasz presentation

		return services;
	}
}