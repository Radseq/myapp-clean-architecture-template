using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Presentation.ErrorHandling;

namespace MyApp.Presentation.Http;

public static class MessageResultHttpExtensions
{
    public static IActionResult ToActionResult(this ControllerBase c, MessageResult r)
    {
        if (r.HasFailed)
            return c.ToProblemDetails(r);

        var localizer = c.HttpContext.RequestServices.GetRequiredService<IApiErrorLocalizer>();
        var warnings = localizer.Localize(r.Warnings);

        return c.Ok(new ApiResponse<object?>(null, warnings));
    }

    public static IActionResult ToActionResult<T>(this ControllerBase c, MessageResult<T> r)
    {
        if (r.HasFailed)
            return c.ToProblemDetails(r);

        var localizer = c.HttpContext.RequestServices.GetRequiredService<IApiErrorLocalizer>();
        var warnings = localizer.Localize(r.Warnings);

        return c.Ok(new ApiResponse<T>(r.Value, warnings));
    }

    public static IActionResult ToCreatedAtActionResult<T>(
        this ControllerBase c,
        MessageResult<T> r,
        string actionName,
        object? routeValues)
    {
        if (r.HasFailed)
            return c.ToProblemDetails(r);

        var localizer = c.HttpContext.RequestServices.GetRequiredService<IApiErrorLocalizer>();
        var warnings = localizer.Localize(r.Warnings);

        // nadal 201 – zasób powstał; ostrzeżenia mówią “co dalej nie wyszło”
        return c.CreatedAtAction(actionName, routeValues, new ApiResponse<T>(r.Value, warnings));
    }

    public static ObjectResult ToProblemDetails(this ControllerBase c, MessageResult r, int? statusOverride = null)
    {
        var factory = c.HttpContext.RequestServices.GetRequiredService<IApiProblemDetailsFactory>();
        var pd = factory.CreateForFailure(c.HttpContext, r, statusOverride);

        return new ObjectResult(pd) { StatusCode = pd.Status };
    }

    public static async Task<IActionResult> SendOk<TIn, TOut>(
        this ControllerBase c,
        IMediator mediator,
        IRequest<MessageResult<TIn>> request,
        Func<TIn, TOut> map,
        CancellationToken ct)
    {
        var result = await mediator.Send(request, ct);
        return c.ToActionResult(result.Map(v => map(v!)));
    }

    public static async Task<IActionResult> SendNoContent(
        this ControllerBase controller,
        IMediator mediator,
        IRequest<MessageResult> request,
        CancellationToken ct)
    {
        var result = await mediator.Send(request, ct);

        if (result.HasFailed)
            return controller.ToProblemDetails(result);

        // Partial => 200 + warnings (dokładnie pod Twój case)
        if (result.IsPartial && result.Warnings.Count > 0)
            return controller.ToActionResult(result);

        return controller.NoContent(); // 204 tylko dla “czystego” Success
    }

    public static async Task<IActionResult> SendCreatedAtAction<TIn, TDto>(
        this ControllerBase controller,
        IMediator mediator,
        IRequest<MessageResult<TIn>> request,
        Func<TIn, TDto> mapDto,
        string getByIdActionName,
        Func<TDto, object> routeValuesFactory,
        CancellationToken ct)
    {
        var result = await mediator.Send(request, ct);

        if (result.HasFailed)
            return controller.ToProblemDetails(result);

        var mapped = result.Map(v => mapDto(v!));

        return controller.ToCreatedAtActionResult(
            mapped,
            getByIdActionName,
            routeValuesFactory(mapped.Value!)
        );
    }
}