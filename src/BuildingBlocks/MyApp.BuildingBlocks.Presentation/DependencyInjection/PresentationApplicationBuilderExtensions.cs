using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using MyApp.BuildingBlocks.Presentation.Diagnostics;
using MyApp.BuildingBlocks.Presentation.Observability.Middleware;
using System.Globalization;

namespace MyApp.BuildingBlocks.Presentation.DependencyInjection;

public static class PresentationApplicationBuilderExtensions
{
	public static WebApplication UsePresentation(this WebApplication app)
	{
		var supportedCultures = new[]
		{
			new CultureInfo("pl-PL"),
			new CultureInfo("en-US"),
		};

		app.UseRequestLocalization(new RequestLocalizationOptions
		{
			DefaultRequestCulture = new RequestCulture("pl-PL"),
			SupportedCultures = supportedCultures,
			SupportedUICultures = supportedCultures
		});

		app.UseSwagger();
		app.UseSwaggerUI(options =>
		{
			var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
			foreach (var description in provider.ApiVersionDescriptions)
			{
				options.SwaggerEndpoint(
					$"/swagger/{description.GroupName}/swagger.json",
					description.GroupName.ToUpperInvariant());
			}
		});

		// kolejność – correlation zawsze pierwszy
		app.UseMiddleware<CorrelationIdMiddleware>();

		//if (app.Environment.IsDevelopment())
		//    app.UseHttpLogging();

		app.UseMiddleware<RequestLoggingMiddleware>();
		app.UseMiddleware<BodyOnErrorLoggingMiddleware>();
		app.UseMiddleware<GlobalExceptionMiddleware>();

		app.MapControllers();
		app.MapLoggingDiagnostics();

		return app;
	}
}