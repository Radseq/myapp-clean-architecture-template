using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MyApp.BuildingBlocks.Domain.Common;
using MyApp.BuildingBlocks.Presentation.ErrorHandling;
using MyApp.BuildingBlocks.Presentation.Observability.Middleware;

namespace MyApp.BuildingBlocks.Presentation.Http;

public static class MessageResultHttpExtensions
{
	public static IActionResult ToActionResult(this ControllerBase c, MessageResult r)
	{
		ApplyDiagnostics(c.HttpContext, r);

		if (r.HasFailed)
			return c.ToProblemDetails(r);

		var localizer = c.HttpContext.RequestServices.GetRequiredService<IApiErrorLocalizer>();
		var warnings = localizer.Localize(r.Warnings);

		return c.Ok(new ApiResponse<object?>(null, warnings));
	}

	public static IActionResult ToActionResult<T>(this ControllerBase c, MessageResult<T> r)
	{
		ApplyDiagnostics(c.HttpContext, r);

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
		ApplyDiagnostics(c.HttpContext, r);

		if (r.HasFailed)
			return c.ToProblemDetails(r);

		var localizer = c.HttpContext.RequestServices.GetRequiredService<IApiErrorLocalizer>();
		var warnings = localizer.Localize(r.Warnings);

		return c.CreatedAtAction(actionName, routeValues, new ApiResponse<T>(r.Value, warnings));
	}

	public static ObjectResult ToProblemDetails(this ControllerBase c, MessageResult r, int? statusOverride = null)
	{
		ApplyDiagnostics(c.HttpContext, r);

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

		ApplyDiagnostics(controller.HttpContext, result);

		if (result.HasFailed)
			return controller.ToProblemDetails(result);

		// Jeśli są warnings -> 200 + ApiResponse(null, warnings)
		if (result.Warnings.Count > 0)
			return controller.ToActionResult(result);

		return controller.NoContent();
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

		ApplyDiagnostics(controller.HttpContext, result);

		if (result.HasFailed)
			return controller.ToProblemDetails(result);

		var mapped = result.Map(v => mapDto(v!));

		return controller.ToCreatedAtActionResult(
			mapped,
			getByIdActionName,
			routeValuesFactory(mapped.Value!)
		);
	}

	private static void ApplyDiagnostics(HttpContext ctx, MessageResult r)
	{
		BodyLogPolicyHttpContext.Set(ctx, r.Diagnostics.BodyLogPolicy);
	}
}