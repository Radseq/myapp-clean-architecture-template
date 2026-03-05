using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.BuildingBlocks.Presentation.ErrorHandling;

public interface IApiProblemDetailsFactory
{
    ProblemDetails CreateForFailure(HttpContext ctx, MessageResult result, int? statusOverride = null);
    ProblemDetails CreateForException(HttpContext ctx, Exception ex);
}
