using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.BuildingBlocks.Application.Abstractions.Outbox;
using MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

namespace MyApp.BuildingBlocks.Infrastructure.Outbox;

public static class OutboxServiceCollectionExtensions
{
	public static IServiceCollection AddEfOutbox<TModule, TDbContext, TMsg>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionPath,
		string? optionsName = null)
		where TDbContext : DbContext
		where TMsg : class, IOutboxMessageEntity, new()
	{
		optionsName ??= typeof(TModule).Name;

		services.AddSingleton<IOutboxModuleOptions<TModule>>(
			new OutboxModuleOptions<TModule>(optionsName));

		services.AddOptions<OutboxOptions>(optionsName)
			.Bind(configuration.GetSection(sectionPath))
			.Validate(o => o.BatchSize > 0, "Outbox.BatchSize must be > 0");

		services.AddScoped<IOutboxWriter<TModule>, EfOutboxWriter<TDbContext, TMsg, TModule>>();
		services.AddScoped<IOutboxDispatcher<TModule>, OutboxDispatcher<TDbContext, TMsg, TModule>>();
		services.AddScoped<OutboxProcessor<TDbContext, TMsg, TModule>>();

		services.AddHostedService<OutboxWorker<TDbContext, TMsg, TModule>>();

		return services;
	}
}