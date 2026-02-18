using Asp.Versioning.ApiExplorer;
using FluentValidation;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MyApp.Api.Common;
using MyApp.Api.Common.ProblemDetailsOthers;
using MyApp.Api.Logging;
using MyApp.Api.Mappings;
using MyApp.Api.Middleware;
using MyApp.Api.Swagger;
using MyApp.Application.Abstractions.Observability;
using MyApp.Application.Abstractions.Transport;
using MyApp.Application.Common.Behaviors;
using MyApp.Infrastructure;
using MyApp.Infrastructure.ExternalServices;
using MyApp.Infrastructure.Observability;
using MyApp.Infrastructure.Outbox;
using MyApp.Infrastructure.Persistence.DbFirst;
using NLog;
using System.Globalization;

var bootstrapLogger = MyAppLoggingExtensions.CreateBootstrapLogger("nlog.config");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddMyAppProductionLogging();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddHttpContextAccessor();
    // musi być po AddHttpContextAccessor
    builder.Services.Configure<RequestLoggingOptions>(
    builder.Configuration.GetSection(RequestLoggingOptions.SectionName));
    // end

    // ProblemDetails boundary
    builder.Services.AddSingleton<IApiErrorLocalizer, ApiErrorLocalizer>();
    builder.Services.AddSingleton<IApiErrorHttpStatusMapper, DefaultApiErrorHttpStatusMapper>();
    builder.Services.AddSingleton<IApiProblemDetailsFactory, ApiProblemDetailsFactory>();

    builder.Services.Configure<ApiErrorHttpStatusOptions>(opt =>
    {
        // Validation
        opt.CodeToStatus[1000] = StatusCodes.Status400BadRequest;
        opt.CodeToStatus[1001] = StatusCodes.Status400BadRequest;

        // NotFound (orders/customers)
        opt.CodeToStatus[2001] = StatusCodes.Status404NotFound;
        opt.CodeToStatus[2101] = StatusCodes.Status404NotFound;

        // Business/BadRequest
        opt.CodeToStatus[2002] = StatusCodes.Status400BadRequest;

        // db
        opt.CodeToStatus[3001] = StatusCodes.Status409Conflict;              // Conflict
        opt.CodeToStatus[3002] = StatusCodes.Status500InternalServerError;   // Unexpected
        opt.CodeToStatus[3003] = StatusCodes.Status500InternalServerError;   // PendingChanges (server misuse)
        opt.CodeToStatus[3004] = StatusCodes.Status409Conflict;              // Duplicate
        opt.CodeToStatus[3005] = StatusCodes.Status409Conflict;              // ForeignKey
        opt.CodeToStatus[3006] = StatusCodes.Status500InternalServerError;   // ExecutionStrategyRequires...

        // Transport errors
        opt.CodeToStatus[4001] = StatusCodes.Status502BadGateway;
        opt.CodeToStatus[4002] = StatusCodes.Status504GatewayTimeout;
        opt.CodeToStatus[4003] = StatusCodes.Status502BadGateway;

        // Common unexpected
        opt.CodeToStatus[5000] = StatusCodes.Status500InternalServerError;
    });

    // Correlation outbound
    builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

    //builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(builder.Configuration.GetConnectionString("Default")));

    //builder.Services.AddDbContext<AppDbContext>(options =>
    //    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
    //            .LogTo(Console.WriteLine, LogLevel.Information) // Log to console
    //            .EnableSensitiveDataLogging() // Show parameter values
    //            .EnableDetailedErrors()); // Get more error details);

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("Default"),
            sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), errorNumbersToAdd: null));

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });


    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(MyApp.Application.ApplicationAssemblyMarker).Assembly);

        cfg.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        cfg.AddOpenBehavior(typeof(QueryCachingBehavior<,>));
        cfg.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
    });



    builder.Services.AddValidatorsFromAssembly(typeof(MyApp.Application.Orders.Commands.CreateOrder.CreateOrderAndDispatchTransportValidator).Assembly);

    builder.Services.AddAutoMapper(typeof(OrdersApiMappingProfile).Assembly);

    builder.Services.AddSwaggerWithVersioning();

    builder.Services.AddInfrastructure(builder.Configuration);

    // Transport integration
    builder.Services.AddHttpClient<ITransportApiClient, TransportApiClient>(http =>
    {
        var baseUrl = builder.Configuration["TransportApi:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            http.BaseAddress = new Uri(baseUrl);

        http.Timeout = TimeSpan.FromSeconds(10);
    }).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

    builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");

    builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection("Outbox"));


    builder.Services.Configure<BodyLoggingOptions>(
        builder.Configuration.GetSection(BodyLoggingOptions.SectionName));


    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddHttpLogging(o =>
        {
            o.CombineLogs = true; // one log entry for request+response (ASP.NET Core 8+)
            o.LoggingFields =
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod |
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath |
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestQuery |
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestHeaders |
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestBody |
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode |
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseBody;

            o.RequestBodyLogLimit = 4096;
            o.ResponseBodyLogLimit = 4096;

            // Treat JSON as text so body is logged
            o.MediaTypeOptions.AddText("application/json");
            o.MediaTypeOptions.AddText("application/problem+json");

            // VERY IMPORTANT: don't log secrets
            o.RequestHeaders.Remove("Authorization");
            o.RequestHeaders.Remove("Cookie");
            o.ResponseHeaders.Remove("Set-Cookie");
        });
    }


    var app = builder.Build();

    var supportedCultures = new[]
    {
    new CultureInfo("pl-PL"),
    new CultureInfo("en-US")
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

    app.UseMiddleware<CorrelationIdMiddleware>();       // najpierw - żeby scope/logi miały CorrelationId
    if (app.Environment.IsDevelopment())
    {
        app.UseHttpLogging();
    }
    app.UseMiddleware<BodyOnErrorLoggingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();

    //app.UseRouting();
    //app.UseAuthentication();
    //app.UseAuthorization();
    app.MapControllers();

    app.MapLoggingDiagnostics();

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
