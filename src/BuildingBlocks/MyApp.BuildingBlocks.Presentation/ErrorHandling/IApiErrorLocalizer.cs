using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.BuildingBlocks.Presentation.ErrorHandling;

public interface IApiErrorLocalizer
{
	ErrorData Localize(ErrorData e);
	IReadOnlyList<ErrorData> Localize(IReadOnlyList<ErrorData> list);
}
