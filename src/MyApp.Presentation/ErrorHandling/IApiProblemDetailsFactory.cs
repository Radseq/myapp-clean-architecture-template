using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyApp.Domain.Common;

namespace MyApp.Presentation.ErrorHandling;

public interface IApiProblemDetailsFactory
{
    ProblemDetails CreateForFailure(HttpContext ctx, MessageResult result, int? statusOverride = null);
    ProblemDetails CreateForException(HttpContext ctx, Exception ex);
}
