using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Modules.Orders.Presentation;

public static class DependencyInjection
{
	public static IMvcBuilder AddOrdersPresentation(this IMvcBuilder mvc)
	{
		mvc.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(OrdersPresentationAssemblyMarker).Assembly));
		return mvc;
	}

	public static IServiceCollection AddOrdersPresentation(this IServiceCollection services)
	{
		services.AddAutoMapper(typeof(OrdersPresentationAssemblyMarker).Assembly);
		return services;
	}
}