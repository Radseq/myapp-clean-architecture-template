using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.BuildingBlocks.Application;
using MyApp.BuildingBlocks.Infrastructure;
using MyApp.BuildingBlocks.Presentation.DependencyInjection;
using MyApp.BuildingBlocks.Presentation.Diagnostics;
using MyApp.Host.Diagnostics;
using MyApp.Modules.Orders;
using MyApp.Modules.Transport;
using NLog;

var bootstrapLogger = MyAppLoggingExtensions.CreateBootstrapLogger("nlog.config");

try
{
	var builder = WebApplication.CreateBuilder(args);

	var cfg = builder.Configuration;
	var env = builder.Environment;
	var services = builder.Services;

	builder.AddMyAppProductionLogging();

	services.TryAddSingleton<ILoggingDiagnosticsProvider, NLogLoggingDiagnosticsProvider>();

	// MVC tylko raz i na początku
	var mvc = services.AddControllers();

	// BuildingBlocks
	services.AddBuildingBlocksApplication();
	services.AddInfrastructure(cfg, env);
	services.AddPresentation(cfg, env, mvc);

	// Modules
	services.AddOrdersModule(cfg, mvc, env);
	services.AddTransportModule(cfg, mvc);

	var app = builder.Build();

	app.UsePresentation();

	await app.RunAsync();
}
catch (Exception ex)
{
	bootstrapLogger.Error(ex, "Host terminated unexpectedly");
	throw;
}
finally
{
	LogManager.Shutdown();
}