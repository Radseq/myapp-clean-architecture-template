using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.BuildingBlocks.Presentation.Http;

/// <summary>
/// Spójny kontrakt dla sukcesu: zawsze { value, warnings }.
/// Błędy idą jako ProblemDetails.
/// </summary>
public sealed record ApiResponse<T>(T? Value, IReadOnlyList<ErrorData> Warnings);
