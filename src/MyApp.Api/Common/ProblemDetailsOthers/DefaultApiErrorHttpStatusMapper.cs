using Microsoft.Extensions.Options;
using MyApp.Domain.Common;

namespace MyApp.Api.Common.ProblemDetailsOthers;

public sealed class DefaultApiErrorHttpStatusMapper(IOptions<ApiErrorHttpStatusOptions> options) : IApiErrorHttpStatusMapper
{
    private readonly ApiErrorHttpStatusOptions _opt = options.Value;

    public int DecideStatusCode(IReadOnlyList<ErrorData> errors)
    {
        if (errors.Count == 0)
            return _opt.DefaultFailureStatus;

        var statuses = errors
            .Select(ResolveSingle)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .Distinct()
            .ToArray();

        if (statuses.Length == 0)
            return _opt.DefaultFailureStatus;

        if (statuses.Length == 1)
            return statuses[0];

        if (statuses.Any(s => s >= 500))
            return StatusCodes.Status500InternalServerError;

        if (statuses.Any(s => s == StatusCodes.Status409Conflict))
            return StatusCodes.Status409Conflict;

        if (statuses.Any(s => s == StatusCodes.Status403Forbidden))
            return StatusCodes.Status403Forbidden;

        if (statuses.Any(s => s == StatusCodes.Status401Unauthorized))
            return StatusCodes.Status401Unauthorized;

        if (statuses.All(s => s == StatusCodes.Status404NotFound))
            return StatusCodes.Status404NotFound;

        return _opt.DefaultFailureStatus;
    }

    private int? ResolveSingle(ErrorData e)
    {
        if (_opt.CodeToStatus.TryGetValue(e.Code, out var byCode))
            return byCode;

        if (!string.IsNullOrWhiteSpace(e.Key) && _opt.KeyToStatus.TryGetValue(e.Key, out var byKey))
            return byKey;

        var k = e.Key ?? "";

        // not found
        if (k.Contains("not_found", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status404NotFound;

        // validation
        if (k.Contains("validation", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status400BadRequest;

        // conflict-ish
        if (k.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("foreign_key", StringComparison.OrdinalIgnoreCase) ||
            k.Contains("already", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status409Conflict;

        // auth
        if (k.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status403Forbidden;

        if (k.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status401Unauthorized;

        // downstream
        if (k.Contains("transport", StringComparison.OrdinalIgnoreCase) &&
            (k.Contains("api_failed", StringComparison.OrdinalIgnoreCase) ||
             k.Contains("api_exception", StringComparison.OrdinalIgnoreCase)))
            return StatusCodes.Status502BadGateway;

        if (k.Contains("transport", StringComparison.OrdinalIgnoreCase) &&
            k.Contains("api_canceled", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status504GatewayTimeout;

        // unexpected
        if (k.Contains("unexpected", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status500InternalServerError;

        // warnings
        if (k.Contains("warnings.", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status202Accepted; // albo 200/207

        return null;
    }
}
