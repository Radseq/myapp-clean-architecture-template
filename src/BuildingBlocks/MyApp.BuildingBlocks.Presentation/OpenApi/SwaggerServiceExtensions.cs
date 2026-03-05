using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MyApp.BuildingBlocks.Presentation.OpenApi;

public static class SwaggerServiceExtensions
{
    public static IServiceCollection AddSwaggerWithVersioning(this IServiceCollection services)
    {
        //services
        //    .AddApiVersioning(o =>
        //    {
        //        o.DefaultApiVersion = new ApiVersion(1, 0);
        //        o.AssumeDefaultVersionWhenUnspecified = true;
        //        o.ReportApiVersions = true;
        //        o.ApiVersionReader = new UrlSegmentApiVersionReader();
        //    })
        //    .AddApiExplorer(o =>
        //    {
        //        o.GroupNameFormat = "'v'VVV";
        //        o.SubstituteApiVersionInUrl = true;
        //    });

        services.AddApiVersioning(
               options =>
               {
                   options.AssumeDefaultVersionWhenUnspecified = true;
                   options.DefaultApiVersion = new ApiVersion(1, 0);
                   // reporting api versions will return the headers
                   // "api-supported-versions" and "api-deprecated-versions"
                   options.ReportApiVersions = true;
                   options.Policies.Sunset(0.9)
                   .Effective(DateTimeOffset.Now.AddDays(60))
                   .Link("policy.html")
                       .Title("Versioning Policy")
                       .Type("text/html");

                   // skąd czytać wersję (możesz zostawić tylko UrlSegmentApiVersionReader)
                   //options.ApiVersionReader = ApiVersionReader.Combine(
                   //    new UrlSegmentApiVersionReader(),
                   //    new HeaderApiVersionReader("X-Api-Version"));
               })
            .AddMvc(options =>
            {
                // Automatycznie: Controllers.V1 => 1.0, Controllers.V2 => 2.0, Controllers.V1_1 => 1.1, itd.
                options.Conventions.Add(new VersionByNamespaceConvention());
            })
           .AddApiExplorer(
               options =>
               {
                   // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                   // note: the specified format code will format the version as "'v'major[.minor][-status]"
                   options.GroupNameFormat = "'v'VVV";

                   // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                   // can also be used to control the format of the API version in route templates
                   options.SubstituteApiVersionInUrl = true;
               });

        services.AddSwaggerGen(c =>
        {
            c.OperationFilter<AcceptLanguageHeaderOperationFilter>();
			c.EnableAnnotations();
            // ważne przy wielu dokumentach:
            c.DocInclusionPredicate((docName, apiDesc) => apiDesc.GroupName == docName);
        });

        services.ConfigureOptions<ConfigureSwaggerOptions>();
        return services;
    }

    public sealed class AcceptLanguageHeaderOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();

            if (operation.Parameters.Any(p =>
                    p.In == ParameterLocation.Header &&
                    string.Equals(p.Name, "Accept-Language", StringComparison.OrdinalIgnoreCase)))
                return;

            var schema = new OpenApiSchema();

            // Ustaw "string" niezależnie czy schema.Type to string czy enum (JsonSchemaType?)
            var typeProp = schema.GetType().GetProperty("Type");
            if (typeProp is not null)
            {
                var t = typeProp.PropertyType;
                var underlying = Nullable.GetUnderlyingType(t) ?? t;

                if (underlying == typeof(string))
                    typeProp.SetValue(schema, "string");
                else if (underlying.IsEnum)
                    typeProp.SetValue(schema, Enum.Parse(underlying, "String", ignoreCase: true));
            }

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept-Language",
                In = ParameterLocation.Header,
                Required = false,
                Description = "np. pl-PL albo en-US",
                Schema = schema
            });
        }
    }
}
