using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.BuildingBlocks.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Application;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Infrastructure;
using MyApp.Modules.Orders.Presentation;

namespace MyApp.Modules.Orders;

public static class OrdersModule
{
	public static IServiceCollection AddOrdersModule(
		this IServiceCollection services,
		IConfiguration configuration,
		IMvcBuilder mvc,
		IHostEnvironment env)
	{
		services.AddOrdersApplication();
		services.AddOrdersInfrastructure(configuration, env);
		services.AddOrdersPresentation();
		mvc.AddOrdersPresentation();

		// UoW routing: request assembly -> typed UoW service
		services.AddSingleton<IUnitOfWorkRoute>(
			new UnitOfWorkRoute(
				RequestsAssembly: typeof(OrdersApplicationAssemblyMarker).Assembly,
				UnitOfWorkServiceType: typeof(IOrdersUnitOfWork)));

		return services;
	}
}