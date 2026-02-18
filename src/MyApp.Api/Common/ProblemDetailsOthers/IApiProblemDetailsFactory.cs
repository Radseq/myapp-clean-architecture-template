using Microsoft.AspNetCore.Mvc;
using MyApp.Domain.Common;

namespace MyApp.Api.Common.ProblemDetailsOthers;

public interface IApiProblemDetailsFactory
{
    ProblemDetails CreateForFailure(HttpContext ctx, MessageResult result, int? statusOverride = null);
    ProblemDetails CreateForException(HttpContext ctx, Exception ex);
}
