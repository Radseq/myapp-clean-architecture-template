using MyApp.Domain.Common;

namespace MyApp.Api.Common.ProblemDetailsOthers;

public interface IApiErrorLocalizer
{
    ErrorData Localize(ErrorData e);
    IReadOnlyList<ErrorData> Localize(IReadOnlyList<ErrorData> list);
}
