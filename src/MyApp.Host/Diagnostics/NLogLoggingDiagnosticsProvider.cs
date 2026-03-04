using MyApp.BuildingBlocks.Presentation.Diagnostics;
using NLog;

namespace MyApp.Host.Diagnostics;

public sealed class NLogLoggingDiagnosticsProvider : ILoggingDiagnosticsProvider
{
    public object GetProviderInfo()
    {
        var cfg = LogManager.Configuration;

        var targets = cfg?.AllTargets
            .Select(t => new { t.Name, Type = t.GetType().Name })
            .ToArray() ?? [];

        return new
        {
            provider = "NLog",
            targets
        };
    }
}