using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.BuildingBlocks.Infrastructure.Http;
using MyApp.BuildingBlocks.Infrastructure.Outbox;
using MyApp.IntegrationContracts.Outbox;
using MyApp.Modules.Orders.Application.Abstractions.Persistence;
using MyApp.Modules.Orders.Infrastructure.Persistence;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst;
using MyApp.Modules.Orders.Infrastructure.Persistence.MicrosoftSqlServer.DbFirst.Entities;
using MyApp.Modules.Orders.Infrastructure.Repositories;

namespace MyApp.Modules.Orders.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services,
		IConfiguration cfg, IHostEnvironment env)
	{
		services.AddDbContext<OrdersDbContext>(options =>
		{
			options.UseSqlServer(
				cfg.GetConnectionString("OrdersDb"),
				sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), errorNumbersToAdd: null));

			if (env.IsDevelopment())
			{
				options.EnableSensitiveDataLogging();
				options.EnableDetailedErrors();
			}
		});

		services.AddAutoMapper(typeof(OrdersInfrastructureAssemblyMarker).Assembly);

		services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();

		services.AddScoped<ICustomerReadRepository, CustomerReadRepository>();
		services.AddScoped<IOrderReadRepository, OrderReadRepository>();
		services.AddScoped<IOrderDomainRepository, OrderDomainRepository>();

		// Outbox OWNER = Orders (worker + writer + dispatcher + processor)
		services.AddEfOutbox<OutboxOwners.Orders, OrdersDbContext, OrdersOutboxMessage>(
			cfg,
			sectionPath: "Outbox:Orders",
			optionsName: "Orders");


		// Correlation outbound
		// do dodania gdy będę gadał z innym swoim api, tutaj dla przykładu, order gada z tranport api
		/*
		services.AddHttpClient<ITransportApiClient, TransportApiClient>(http =>
        {
            var baseUrl = cfg["TransportApi:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                http.BaseAddress = new Uri(baseUrl);

            http.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
		 * */

		return services;
	}
}