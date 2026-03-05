using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Modules.Orders.Application;

public static class DependencyInjection
{
	public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
	{
		services.AddMediatR(cfg =>
			cfg.RegisterServicesFromAssembly(typeof(OrdersApplicationAssemblyMarker).Assembly));

		services.AddValidatorsFromAssembly(typeof(OrdersApplicationAssemblyMarker).Assembly);

		return services;
	}
}