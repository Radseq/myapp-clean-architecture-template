using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyApp.Presentation.ErrorHandling;
using System.Diagnostics;

namespace MyApp.Presentation.Observability.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IApiProblemDetailsFactory problemFactory)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            ctx.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception ex)
        {
            var correlationId =
                CorrelationIdMiddleware.TryGet(ctx)
                ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

            var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

            var userId =
               ctx.User?.FindFirst("sub")?.Value
               ?? ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            logger.LogError(
                ex,
                "Unhandled exception. {Method} {Path} CorrelationId={CorrelationId} TraceId={TraceId} UserId={UserId} RemoteIp={RemoteIp}",
                ctx.Request.Method,
                ctx.Request.Path.Value,
                correlationId,
                traceId,
                userId,
                ctx.Connection.RemoteIpAddress?.ToString());


            var pd = problemFactory.CreateForException(ctx, ex);

            ctx.Response.Clear();
            ctx.Response.StatusCode = pd.Status ?? StatusCodes.Status500InternalServerError;
            ctx.Response.ContentType = "application/problem+json";

            await ctx.Response.WriteAsJsonAsync(pd, cancellationToken: ctx.RequestAborted);
        }
    }
}