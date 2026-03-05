using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyApp.BuildingBlocks.Presentation.ErrorHandling;
using System.Diagnostics;

namespace MyApp.BuildingBlocks.Presentation.Observability.Middleware;

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
            var userId =
               ctx.User?.FindFirst("sub")?.Value
               ?? ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            logger.LogError(
                LogEvents.UnhandledException,
                ex,
                "Unhandled exception. UserId={UserId} RemoteIp={RemoteIp}",
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