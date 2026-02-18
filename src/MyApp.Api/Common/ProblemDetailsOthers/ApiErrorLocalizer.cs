using Microsoft.Extensions.Localization;
using MyApp.Domain.Common;

namespace MyApp.Api.Common.ProblemDetailsOthers;

public sealed class ApiErrorLocalizer : IApiErrorLocalizer
{
    private readonly IStringLocalizer _localizer;

    public ApiErrorLocalizer(IStringLocalizerFactory factory)
    {
        var assemblyName = typeof(ApiErrorLocalizer).Assembly.GetName().Name!;
        _localizer = factory.Create("Errors", assemblyName);
    }

    public IReadOnlyList<ErrorData> Localize(IReadOnlyList<ErrorData> list)
        => list.Count == 0 ? [] : list.Select(Localize).ToArray();

    public ErrorData Localize(ErrorData e)
    {
        // 1) spróbuj RESX (nadpisuje opis nawet jeśli Description jest ustawione)
        var localized = TryLocalize(e);
        if (!string.IsNullOrWhiteSpace(localized))
            e = e.WithDescription(localized);

        // 2) fallback: jeśli nadal pusto -> Key
        if (string.IsNullOrWhiteSpace(e.Description))
            e = e.WithDescription(e.Key);

        // 3) nested
        if (e.ExtendedErrors.Count == 0)
            return e;

        var nested = e.ExtendedErrors.Select(Localize).ToArray();
        return e with { ExtendedErrors = nested };
    }

    private string? TryLocalize(ErrorData e)
    {
        if (string.IsNullOrWhiteSpace(e.Key))
            return null;

        try
        {
            var ls = (e.Args is { Length: > 0 })
                ? _localizer[e.Key, e.Args!]
                : _localizer[e.Key];

            return ls.ResourceNotFound ? null : ls.Value;
        }
        catch
        {
            return null;
        }
    }
}
