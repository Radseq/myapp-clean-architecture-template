using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.BuildingBlocks.Application.Abstractions.Observability;

namespace MyApp.BuildingBlocks.Infrastructure.Observability.Persistence;

public static class DependencyInjection
{
	public static IServiceCollection AddObservabilityPayloadStore(this IServiceCollection services, IConfiguration cfg)
	{
		var mode = cfg["Observability:BodyLogging:Store:Mode"];

		if (string.Equals(mode, "Redis", StringComparison.OrdinalIgnoreCase))
		{
			var redisConn = cfg.GetConnectionString("Redis");
			if (string.IsNullOrWhiteSpace(redisConn))
				throw new InvalidOperationException("ConnectionStrings:Redis is required when BodyLogging store mode is Redis.");

			services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
			services.Replace(ServiceDescriptor.Singleton<IFailedHttpPayloadStore, RedisFailedHttpPayloadStore>());
		}
		else if (string.Equals(mode, "Sql", StringComparison.OrdinalIgnoreCase))
		{
			var sqlConn = cfg.GetConnectionString("Default");
			if (string.IsNullOrWhiteSpace(sqlConn))
				throw new InvalidOperationException("ConnectionStrings:SqlServer is required when BodyLogging store mode is Sql.");

			services.AddDbContext<ObservabilityDbContext>(o => o.UseSqlServer(sqlConn));

			// Singleton bo middleware
			services.Replace(ServiceDescriptor.Singleton<IFailedHttpPayloadStore, HttpPayloadStoreWriteRepository>());
		}
		else
		{
			services.Replace(ServiceDescriptor.Singleton<IFailedHttpPayloadStore, NullFailedHttpPayloadStore>());
		}

		return services;
	}
}