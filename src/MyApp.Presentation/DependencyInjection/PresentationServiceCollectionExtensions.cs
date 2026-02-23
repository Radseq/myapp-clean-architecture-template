using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.Presentation.ErrorHandling;
using MyApp.Presentation.Mappings;
using MyApp.Presentation.OpenApi;

namespace MyApp.Presentation.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        services
            .AddControllers()
            .AddApplicationPart(typeof(PresentationAssemblyMarker).Assembly);

        services.AddEndpointsApiExplorer();

        // ProblemDetails boundary
        services.AddSingleton<IApiErrorLocalizer, ApiErrorLocalizer>();
        services.AddSingleton<IApiErrorHttpStatusMapper, DefaultApiErrorHttpStatusMapper>();
        services.AddSingleton<IApiProblemDetailsFactory, ApiProblemDetailsFactory>();

        services.AddAutoMapper(typeof(OrdersApiMappingProfile).Assembly);

        services.AddLocalization(o => o.ResourcesPath = "Resources");

        services.AddSwaggerWithVersioning();

        if (env.IsDevelopment())
        {
            services.AddHttpLogging(o =>
            {
                o.CombineLogs = true;
                o.LoggingFields =
                    HttpLoggingFields.RequestMethod |
                    HttpLoggingFields.RequestPath |
                    HttpLoggingFields.RequestQuery |
                    HttpLoggingFields.RequestHeaders |
                    HttpLoggingFields.RequestBody |
                    HttpLoggingFields.ResponseStatusCode |
                    HttpLoggingFields.ResponseBody;

                o.RequestBodyLogLimit = 4096;
                o.ResponseBodyLogLimit = 4096;

                o.MediaTypeOptions.AddText("application/json");
                o.MediaTypeOptions.AddText("application/problem+json");

                o.RequestHeaders.Remove("Authorization");
                o.RequestHeaders.Remove("Cookie");
                o.ResponseHeaders.Remove("Set-Cookie");
            });
        }

        return services;
    }
}