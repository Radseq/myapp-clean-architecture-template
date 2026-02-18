using MyApp.Domain.Common;

namespace MyApp.Api.Common.ProblemDetailsOthers;

public interface IApiErrorHttpStatusMapper
{
    int DecideStatusCode(IReadOnlyList<ErrorData> errors);
}
