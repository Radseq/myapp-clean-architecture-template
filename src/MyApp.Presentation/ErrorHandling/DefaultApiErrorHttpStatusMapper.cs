using System.Net;
using MyApp.Domain.Common;

namespace MyApp.Presentation.ErrorHandling;

public sealed class DefaultApiErrorHttpStatusMapper : IApiErrorHttpStatusMapper
{
    public int DecideStatusCode(IReadOnlyList<ErrorData> errors)
    {
        if (errors is null || errors.Count == 0)
            return (int)HttpStatusCode.InternalServerError;

        var kind = PickDominantKind(errors);

        return kind switch
        {
            ErrorKind.Validation => (int)HttpStatusCode.BadRequest,
            ErrorKind.NotFound => (int)HttpStatusCode.NotFound,
            ErrorKind.Conflict => (int)HttpStatusCode.Conflict,
            ErrorKind.Unauthorized => (int)HttpStatusCode.Unauthorized,
            ErrorKind.Forbidden => (int)HttpStatusCode.Forbidden,
            ErrorKind.DependencyFailure => (int)HttpStatusCode.BadGateway,
            ErrorKind.DependencyTimeout => (int)HttpStatusCode.GatewayTimeout,
            ErrorKind.Unexpected => (int)HttpStatusCode.InternalServerError,
            _ => (int)HttpStatusCode.InternalServerError
        };
    }

    private static ErrorKind PickDominantKind(IReadOnlyList<ErrorData> errors)
    {
        // Priorytet: 500 > dependency > auth > conflict > notfound > validation
        static int P(ErrorKind k) => k switch
        {
            ErrorKind.Unexpected => 100,
            ErrorKind.DependencyTimeout => 90,
            ErrorKind.DependencyFailure => 80,
            ErrorKind.Forbidden => 70,
            ErrorKind.Unauthorized => 60,
            ErrorKind.Conflict => 50,
            ErrorKind.NotFound => 40,
            ErrorKind.Validation => 30,
            _ => 0
        };

        return errors
            .Select(e => e.Kind)
            .OrderByDescending(P)
            .FirstOrDefault();
    }
}