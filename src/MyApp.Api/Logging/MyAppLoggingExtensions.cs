using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Web;
using System.Globalization;

namespace MyApp.Api.Logging;

public static class MyAppLoggingExtensions
{
    /// <summary>
    /// Bootstrap logger łapie wyjątki w trakcie startu hosta (before Build()).
    /// </summary>
    public static Logger CreateBootstrapLogger(string nlogConfigFile = "nlog.config")
    {
        return LogManager.Setup()
            .LoadConfigurationFromFile(nlogConfigFile)
            .GetCurrentClassLogger();
    }

    /// <summary>
    /// Standard produkcyjny:
    /// - NLog provider
    /// - JSON na stdout (K8S-friendly)
    /// - opcjonalny rolling file (domyślnie tylko Windows poza kontenerem)
    /// - scopes włączone (CorrelationId/TraceId/SpanId/RequestId z BeginScope)
    /// - ActivityTrackingOptions dla TraceId/SpanId/Tags/Baggage
    /// </summary>
    public static WebApplicationBuilder AddMyAppProductionLogging(this WebApplicationBuilder builder)
    {
        var opts = builder.Configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>()
                   ?? new LoggingOptions();

        if (!opts.UseNLog)
            return builder;

        // 1) env vars dla nlog.config (żeby config był "portable" między aplikacjami)
        var isContainer = IsRunningInContainer();
        var fileEnabled = opts.FileEnabled ?? OperatingSystem.IsWindows() && !isContainer;

        var logDir = opts.LogDirectory;
        if (string.IsNullOrWhiteSpace(logDir))
            logDir = Path.Combine(AppContext.BaseDirectory, "logs");

        Environment.SetEnvironmentVariable("MYAPP_LOG_FILE_ENABLED", fileEnabled ? "true" : "false");
        Environment.SetEnvironmentVariable("MYAPP_LOG_DIR", logDir);
        Environment.SetEnvironmentVariable("MYAPP_LOG_FILE_MAXARCHIVE", opts.FileMaxArchiveFiles.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("MYAPP_APP", builder.Environment.ApplicationName);
        Environment.SetEnvironmentVariable("MYAPP_ENV", builder.Environment.EnvironmentName);

        // 2) Microsoft Logging -> NLog
        builder.Logging.ClearProviders();

        // pozwalamy filtrom z appsettings ("Logging:LogLevel") decydować co przechodzi
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);

        builder.Logging.Configure(o =>
        {
            o.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId |
                ActivityTrackingOptions.Tags |
                ActivityTrackingOptions.Baggage;
        });

        // 3) upewniamy się, że config NLog jest załadowany wcześnie (fail-fast)
        LogManager.Setup().LoadConfigurationFromFile(opts.NLogConfigFile);

        // 3a) properties typu int NIE mogą brać layout rendererów w XML, ustawiamy je programowo
        var cfg = LogManager.Configuration;
        if (cfg is not null)
        {
            foreach (var ft in cfg.AllTargets.OfType<FileTarget>())
            {
                ft.MaxArchiveFiles = opts.FileMaxArchiveFiles;
            }

            LogManager.ReconfigExistingLoggers();
        }

        // 4) NLog provider
        builder.Host.UseNLog(new NLogAspNetCoreOptions
        {
            IncludeScopes = true,
            RemoveLoggerFactoryFilter = false,
            CaptureMessageTemplates = true,
            CaptureMessageProperties = true,
            CaptureMessageParameters = true,
            CaptureEventId = EventIdCaptureType.EventId | EventIdCaptureType.EventName
        });

        return builder;
    }

    private static bool IsRunningInContainer()
        => string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
