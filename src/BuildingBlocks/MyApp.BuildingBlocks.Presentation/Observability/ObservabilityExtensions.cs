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
}