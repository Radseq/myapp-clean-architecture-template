using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.BuildingBlocks.Domain.Common;
using System.Diagnostics;

namespace MyApp.BuildingBlocks.Application.Common.Behaviors;

public sealed class RequestLoggingBehavior<TRequest, TResponse>(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
	: IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		var name = typeof(TRequest).Name;
		var start = Stopwatch.GetTimestamp();

		try
		{
			var response = await next(cancellationToken);

			var elapsed = Stopwatch.GetElapsedTime(start);

			if (response is MessageResult mr)
			{
				if (mr.HasFailed)
				{
					logger.LogWarning(
						"Request {RequestName} FAILED in {ElapsedMs} ms. Errors={Errors}",
						name,
						elapsed.TotalMilliseconds,
						mr.Errors.Select(e => new { e.Code, e.Key }).ToArray());
				}
				else if (mr.IsPartial)
				{
					logger.LogInformation(
						"Request {RequestName} PARTIAL in {ElapsedMs} ms. Warnings={Warnings}",
						name,
						elapsed.TotalMilliseconds,
						mr.Warnings.Select(w => new { w.Code, w.Key }).ToArray());
				}
				else
				{
					logger.LogInformation(
						"Request {RequestName} OK in {ElapsedMs} ms",
						name,
						elapsed.TotalMilliseconds);
				}

				return response;
			}

			// jeśli jakiś handler zwróci coś innego – log tylko czas
			logger.LogInformation("Request {RequestName} completed in {ElapsedMs} ms", name, elapsed.TotalMilliseconds);
			return response;
		}
		catch (Exception ex)
		{
			var elapsed = Stopwatch.GetElapsedTime(start);
			logger.LogError(ex, "Request {RequestName} threw exception after {ElapsedMs} ms", name, elapsed.TotalMilliseconds);
			throw;
		}
	}
}
