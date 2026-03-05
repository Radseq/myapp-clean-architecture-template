using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Modules.Transport.Application;

public static class DependencyInjection
{
	public static IServiceCollection AddTransportApplication(this IServiceCollection services)
	{
		services.AddMediatR(cfg =>
			cfg.RegisterServicesFromAssembly(typeof(TransportApplicationAssemblyMarker).Assembly));

		services.AddValidatorsFromAssembly(typeof(TransportApplicationAssemblyMarker).Assembly);

		return services;
	}
}