using MyApp.Domain.Common;

namespace MyApp.Presentation.Http;

/// <summary>
/// Spójny kontrakt dla sukcesu: zawsze { value, warnings }.
/// Błędy idą jako ProblemDetails.
/// </summary>
public sealed record ApiResponse<T>(T? Value, IReadOnlyList<ErrorData> Warnings);
