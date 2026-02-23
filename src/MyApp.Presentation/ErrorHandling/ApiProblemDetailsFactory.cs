using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using MyApp.Domain.Common;
using MyApp.Presentation.Observability.Middleware;
using System.Diagnostics;

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

        var title = PickTitle(errors);

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
        var mr = MessageResult.Fail(Application.Common.Errors.Common.Unexpected);

        var pd = CreateForFailure(ctx, mr, statusOverride: StatusCodes.Status500InternalServerError);
        pd.Title = "Unhandled exception";

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

    private static string PickTitle(IReadOnlyList<ErrorData> errors)
    {
        var kind = errors.Count > 0 ? errors[0].Kind : ErrorKind.Unexpected;

        return kind switch
        {
            ErrorKind.Validation => "Validation failed",
            ErrorKind.NotFound => "Not found",
            ErrorKind.Conflict => "Conflict",
            ErrorKind.Unauthorized => "Unauthorized",
            ErrorKind.Forbidden => "Forbidden",
            ErrorKind.DependencyFailure => "Upstream dependency failed",
            ErrorKind.DependencyTimeout => "Upstream dependency timeout",
            _ => "Request failed"
        };
    }
}