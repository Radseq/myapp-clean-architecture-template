using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyApp.Infrastructure.Outbox;

public sealed class OutboxWorker(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    ILogger<OutboxWorker> logger)
    : BackgroundService
{
    private readonly OutboxOptions _opt = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                var processed = await processor.RunOnceAsync(stoppingToken);

                // jak nic nie było do roboty, odczekaj standardowy interwał
                // jak było, to i tak odczekaj (zwykle krótko, np. 2s)
                await Task.Delay(_opt.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox worker loop failed.");
                // awaria worker'a nie powinna robić hot-loop
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
