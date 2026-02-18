namespace MyApp.Api.Common.ProblemDetailsOthers;

public sealed class ApiErrorHttpStatusOptions
{
    // preferuj mapowanie po Code (stabilniejsze niż Key)
    public Dictionary<int, int> CodeToStatus { get; } = [];

    // opcjonalnie mapowanie po Key
    public Dictionary<string, int> KeyToStatus { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int DefaultFailureStatus { get; set; } = StatusCodes.Status400BadRequest;
}
