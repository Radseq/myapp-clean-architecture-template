using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyApp.BuildingBlocks.Application.Abstractions.Persistence;
using MyApp.BuildingBlocks.Application.Common.Behaviors;
using MyApp.BuildingBlocks.Application.Common.Persistence;

namespace MyApp.BuildingBlocks.Application;

public static class DependencyInjection
{
	public static IServiceCollection AddBuildingBlocksApplication(this IServiceCollection services)
	{
		// UoW routing
		services.AddScoped<IUnitOfWorkResolver, UnitOfWorkResolver>();

		// Global pipeline (działa dla wszystkich modułów)
		services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));
		services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
		services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
		services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

		// UWAGA: MediatR rejestrujesz per moduł (nie globalnie), więc tutaj nic nie skanuj.
		// FluentValidation też per moduł.

		return services;
	}
}