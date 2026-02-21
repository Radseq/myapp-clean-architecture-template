using MyApp.Api.Diagnostics;
using MyApp.Infrastructure;
using MyApp.Presentation.DependencyInjection;
using MyApp.Presentation.Diagnostics;
using NLog;

var bootstrapLogger = MyAppLoggingExtensions.CreateBootstrapLogger("nlog.config");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddMyAppProductionLogging();

    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    // Presentation: controllers + swagger + problem details + localization + http logging(dev)
    builder.Services.AddPresentation(builder.Configuration, builder.Environment);

    builder.Services.AddSingleton<ILoggingDiagnosticsProvider, NLogLoggingDiagnosticsProvider>();

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