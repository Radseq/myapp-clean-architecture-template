using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.BuildingBlocks.Presentation.Observability.Options;

namespace MyApp.BuildingBlocks.Presentation.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddMyAppObservability(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<RequestLoggingOptions>(cfg.GetSection(RequestLoggingOptions.SectionName));
        services.Configure<BodyLoggingOptions>(cfg.GetSection(BodyLoggingOptions.SectionName));

        return services;
    }

    ///// <summary>
    ///// Kolejność jest istotna:
    ///// CorrelationId (scope + header) -> RequestLogging (log końcowy) -> BodyOnError (capture) -> GlobalException (produkuje problem+json).
    ///// </summary>
    //public static IApplicationBuilder UseMyAppObservability(this IApplicationBuilder app)
    //{
    //    app.UseMiddleware<CorrelationIdMiddleware>();
    //    app.UseMiddleware<RequestLoggingMiddleware>();
    //    app.UseMiddleware<BodyOnErrorLoggingMiddleware>();
    //    app.UseMiddleware<GlobalExceptionMiddleware>();

    //    return app;
    //}
}