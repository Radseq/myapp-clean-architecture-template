using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MyApp.BuildingBlocks.Application.Abstractions.Caching;
using MyApp.BuildingBlocks.Application.Abstractions.Observability;
using MyApp.BuildingBlocks.Application.Abstractions.Security;
using MyApp.BuildingBlocks.Infrastructure.Caching;
using MyApp.BuildingBlocks.Infrastructure.Http;
using MyApp.BuildingBlocks.Infrastructure.Observability;
using MyApp.BuildingBlocks.Infrastructure.Observability.Persistence;
using MyApp.BuildingBlocks.Infrastructure.Security;
using StackExchange.Redis;

namespace MyApp.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services,
		IConfiguration cfg, IHostEnvironment env)
	{

		services.AddSingleton(TimeProvider.System);


		services.AddSingleton<ICorrelationContext, CorrelationContext>();

		services.AddMemoryCache();

		var caching = cfg.GetSection("Caching").Get<CachingOptions>() ?? new CachingOptions();

		if (caching.UseRedis)
		{
			services.AddSingleton<IConnectionMultiplexer>(_ =>
				ConnectionMultiplexer.Connect(cfg.GetConnectionString("Redis")
					?? throw new InvalidOperationException("Missing ConnectionStrings:Redis")));

			services.AddSingleton(new RedisAppCacheOptions
			{
				KeyPrefix = caching.KeyPrefix,
				// JsonOptions = ... // zostaw null jeśli nie potrzebujesz
			});

			services.AddSingleton<IAppCache, RedisAppCache>();
		}
		else
		{
			services.AddSingleton<IAppCache, MemoryAppCache>();
		}

		services.AddScoped<ICurrentUserService, CurrentUserService>();

		services.TryAddSingleton<IFailedHttpPayloadStore, NullFailedHttpPayloadStore>();

		services.AddTransient<CorrelationIdDelegatingHandler>();

		services.AddObservabilityPayloadStore(cfg);

		return services;
	}
}