using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.BuildingBlocks.Presentation.ErrorHandling;

public interface IApiErrorHttpStatusMapper
{
	int DecideStatusCode(IReadOnlyList<ErrorData> errors);
}
