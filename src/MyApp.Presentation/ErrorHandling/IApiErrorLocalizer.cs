using MyApp.Domain.Common;

namespace MyApp.Presentation.ErrorHandling;

public interface IApiErrorLocalizer
{
    ErrorData Localize(ErrorData e);
    IReadOnlyList<ErrorData> Localize(IReadOnlyList<ErrorData> list);
}
