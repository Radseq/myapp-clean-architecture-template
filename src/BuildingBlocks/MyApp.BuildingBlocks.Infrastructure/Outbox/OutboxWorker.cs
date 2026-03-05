using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.BuildingBlocks.Infrastructure.Outbox.Storage;

namespace MyApp.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxWorker<TDbContext, TMsg, TModule>(
	IServiceScopeFactory scopes,
	IOptionsMonitor<OutboxOptions> options,
	IOutboxModuleOptions<TModule> moduleOpt,
	ILogger<OutboxWorker<TDbContext, TMsg, TModule>> logger)
	: BackgroundService
	where TDbContext : DbContext
	where TMsg : class, IOutboxMessageEntity
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			var opt = options.Get(moduleOpt.OptionsName);

			try
			{
				using var scope = scopes.CreateScope();
				var processor = scope.ServiceProvider
					.GetRequiredService<OutboxProcessor<TDbContext, TMsg, TModule>>();

				_ = await processor.RunOnceAsync(stoppingToken);

				await Task.Delay(opt.PollInterval, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
			catch (Exception ex)
			{
				logger.LogError(ex, "Outbox worker loop failed. Module={Module}", typeof(TModule).Name);
				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}
		}
	}
}