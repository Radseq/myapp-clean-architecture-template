using Microsoft.AspNetCore.Mvc;
using MyApp.Domain.Common;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using MyApp.Presentation.Observability.Middleware;

namespace MyApp.Presentation.ErrorHandling;

public sealed class ApiProblemDetailsFactory(
    IApiErrorLocalizer localizer,
    IApiErrorHttpStatusMapper statusMapper,
    IHostEnvironment env)
    : IApiProblemDetailsFactory
{
    public ProblemDetails CreateForFailure(HttpContext ctx, MessageResult result, int? statusOverride = null)
    {
        var errors = localizer.Localize(result.Errors);
        var warnings = localizer.Localize(result.Warnings);

        var status = statusOverride ?? statusMapper.DecideStatusCode(errors);

        var detail = errors.FirstOrDefault()?.Description
                     ?? errors.FirstOrDefault()?.Key
                     ?? "errors.unknown";

        var isValidation = errors.Any(e =>
            !string.IsNullOrWhiteSpace(e.Key) &&
            e.Key.Contains("validation", StringComparison.OrdinalIgnoreCase));

        var title = isValidation ? "Validation failed" : "Request failed";

        var pd = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };

        pd.Extensions["errors"] = errors;
        if (warnings.Count > 0)
            pd.Extensions["warnings"] = warnings;

        pd.Extensions["traceId"] = Activity.Current?.Id ?? ctx.TraceIdentifier;

        pd.Extensions["correlationId"] =
            CorrelationIdMiddleware.TryGet(ctx)
            ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

        return pd;
    }

    public ProblemDetails CreateForException(HttpContext ctx, Exception ex)
    {
        // “nieprzewidziany wyjątek” traktujemy jak kontrolowany błąd na boundary
        var mr = MessageResult.Fail(Application.Common.Errors.Common.Unexpected);

        var pd = CreateForFailure(ctx, mr, statusOverride: StatusCodes.Status500InternalServerError);
        pd.Title = "Unhandled exception";

        // Bezpieczny debug (tylko DEV)
        if (env.IsDevelopment())
        {
            pd.Extensions["exception"] = new
            {
                type = ex.GetType().FullName,
                ex.Message
            };
        }

        return pd;
    }
}
