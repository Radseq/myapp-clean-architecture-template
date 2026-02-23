using MyApp.Domain.Common;

namespace MyApp.Presentation.ErrorHandling;

public interface IApiErrorHttpStatusMapper
{
    int DecideStatusCode(IReadOnlyList<ErrorData> errors);
}
