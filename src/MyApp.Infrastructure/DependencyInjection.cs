using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.Application.Abstractions.Caching;
using MyApp.Application.Abstractions.Observability;
using MyApp.Application.Abstractions.Outbox;
using MyApp.Application.Abstractions.Persistence;
using MyApp.Application.Abstractions.Security;
using MyApp.Application.Abstractions.Transport;
using MyApp.Infrastructure.Caching;
using MyApp.Infrastructure.ExternalServices;
using MyApp.Infrastructure.ExternalServices.Http;
using MyApp.Infrastructure.Mapping;
using MyApp.Infrastructure.Observability;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Outbox.Handlers;
using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;
using MyApp.Infrastructure.Persistence.Repositories;
using MyApp.Infrastructure.Security;
using StackExchange.Redis;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        IConfiguration cfg, IHostEnvironment env)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(
                cfg.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), errorNumbersToAdd: null));

            if (env.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddAutoMapper(typeof(OrdersReadProfile).Assembly);

        // Persistence
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICustomerReadRepository, CustomerReadRepository>();

        services.AddScoped<IOrderReadRepository, OrderReadRepository>();
        services.AddScoped<IOrderDomainRepository, OrderDomainRepository>();

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddScoped<OutboxProcessor>();

        services.AddHostedService<OutboxWorker>();

        services.AddScoped<IOutboxMessageHandler, TransportOrderCreatedOutboxHandler>();

        services.AddSingleton<ICorrelationContext, CorrelationContext>();
        // Correlation outbound
        services.AddTransient<CorrelationIdDelegatingHandler>();



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

        // register request and response body on error

        var bodyStoreMode = cfg["Observability:BodyLogging:Store:Mode"];

        if (string.Equals(bodyStoreMode, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisConn = cfg.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(redisConn))
                throw new InvalidOperationException("ConnectionStrings:Redis is required when BodyLogging store mode is Redis.");

            services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);

            services.AddSingleton<IFailedHttpPayloadStore, RedisFailedHttpPayloadStore>();
        }
        else if (string.Equals(bodyStoreMode, "Sql", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFailedHttpPayloadStore, EfFailedHttpPayloadStore>);
        }
        else
        {
            services.AddSingleton<IFailedHttpPayloadStore, NullFailedHttpPayloadStore>();
        }

        //end

        // Transport integration
        services.AddHttpClient<ITransportApiClient, TransportApiClient>(http =>
        {
            var baseUrl = cfg["TransportApi:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                http.BaseAddress = new Uri(baseUrl);

            http.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        return services;
    }
}