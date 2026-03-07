using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MyApp.BuildingBlocks.Application.Abstractions.Security;
using MyApp.BuildingBlocks.Presentation.ErrorHandling;
using MyApp.BuildingBlocks.Presentation.Observability;
using MyApp.BuildingBlocks.Presentation.OpenApi;
using MyApp.BuildingBlocks.Presentation.Security;

namespace MyApp.BuildingBlocks.Presentation.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
	public static IServiceCollection AddPresentation(
		this IServiceCollection services,
		IConfiguration config,
		IHostEnvironment env,
		IMvcBuilder mvc)
	{
		// ApplicationPart: BB.Presentation
		mvc.AddApplicationPart(typeof(PresentationAssemblyMarker).Assembly);

		services.AddEndpointsApiExplorer();

        // ProblemDetails boundary
        services.AddSingleton<IApiErrorLocalizer, ApiErrorLocalizer>();
        services.AddSingleton<IApiErrorHttpStatusMapper, DefaultApiErrorHttpStatusMapper>();
        services.AddSingleton<IApiProblemDetailsFactory, ApiProblemDetailsFactory>();

        services.AddLocalization(o => o.ResourcesPath = "Resources");

		services.AddMyAppObservability(config);

		services.AddSwaggerWithVersioning();

		services.AddHttpContextAccessor();

		services.TryAddScoped<ICurrentUserService, CurrentUserService>();

		//if (env.IsDevelopment())
		//{
		//    services.AddHttpLogging(o =>
		//    {
		//        o.CombineLogs = true;
		//        o.LoggingFields =
		//            HttpLoggingFields.RequestMethod |
		//            HttpLoggingFields.RequestPath |
		//            HttpLoggingFields.RequestQuery |
		//            HttpLoggingFields.RequestHeaders |
		//            HttpLoggingFields.RequestBody |
		//            HttpLoggingFields.ResponseStatusCode |
		//            HttpLoggingFields.ResponseBody;

		//        o.RequestBodyLogLimit = 4096;
		//        o.ResponseBodyLogLimit = 4096;

		//        o.MediaTypeOptions.AddText("application/json");
		//        o.MediaTypeOptions.AddText("application/problem+json");

		//        o.RequestHeaders.Remove("Authorization");
		//        o.RequestHeaders.Remove("Cookie");
		//        o.ResponseHeaders.Remove("Set-Cookie");
		//    });
		//}

		return services;
    }
}