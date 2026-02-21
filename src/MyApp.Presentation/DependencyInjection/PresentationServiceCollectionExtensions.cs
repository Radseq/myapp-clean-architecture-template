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

        services.Configure<ApiErrorHttpStatusOptions>(opt =>
        {
            // Validation
            opt.CodeToStatus[1000] = StatusCodes.Status400BadRequest;
            opt.CodeToStatus[1001] = StatusCodes.Status400BadRequest;

            // NotFound
            opt.CodeToStatus[2001] = StatusCodes.Status404NotFound;
            opt.CodeToStatus[2101] = StatusCodes.Status404NotFound;

            // Business
            opt.CodeToStatus[2002] = StatusCodes.Status400BadRequest;

            // db
            opt.CodeToStatus[3001] = StatusCodes.Status409Conflict;
            opt.CodeToStatus[3002] = StatusCodes.Status500InternalServerError;
            opt.CodeToStatus[3003] = StatusCodes.Status500InternalServerError;
            opt.CodeToStatus[3004] = StatusCodes.Status409Conflict;
            opt.CodeToStatus[3005] = StatusCodes.Status409Conflict;
            opt.CodeToStatus[3006] = StatusCodes.Status500InternalServerError;

            // Transport
            opt.CodeToStatus[4001] = StatusCodes.Status502BadGateway;
            opt.CodeToStatus[4002] = StatusCodes.Status504GatewayTimeout;
            opt.CodeToStatus[4003] = StatusCodes.Status502BadGateway;

            // Common unexpected
            opt.CodeToStatus[5000] = StatusCodes.Status500InternalServerError;
        });

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