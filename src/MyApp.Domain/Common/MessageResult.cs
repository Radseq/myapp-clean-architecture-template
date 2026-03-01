namespace MyApp.Domain.Common;

#region MessageResult — koncept i mapowanie na HTTP
/*
 * MessageResult to „kontrolowany wynik use-case’a” (Application), który:
 * - zastępuje wyjątki jako mechanizm flow w logice aplikacyjnej,
 * - niesie spójny kontrakt błędów/ostrzeżeń między warstwami (Application ↔ API),
 * - jest mapowany dopiero na granicy HTTP na ApiResponse<T> lub ApiProblemDetails.
 *
 * Model wyniku:
 * - Ok(value)                      — sukces
 * - Fail(errors)                   — porażka (lista błędów)
 * - Ok(value, warnings) / Partial  — sukces z ostrzeżeniami (partial success)
 *
 * ErrorData (najważniejsze pola):
 * - Code        — stabilny identyfikator (logi, mapowanie statusów, analityka)
 * - Key         — klucz do lokalizacji (np. errors.orders.not_found)
 * - Args        — argumenty do formatowania (np. id)
 * - Description — fallback (jeśli brak wpisu w RESX)
 *
 * Lokalizacja po stronie API:
 *   RESX (Key + Args) > Description > Key
 *
 * Kontrakty i odpowiedzialności:
 * - MessageResult     — wewnętrzny kontrakt aplikacji (bez zależności od HTTP)
 * - ApiProblemDetails — kontrakt HTTP (tylko warstwa API)
 *
 * Partial success (ważne):
 *   np. zapis w DB OK, ale wysyłka do zewnętrznego API nieudana → sukces + warnings.
 *
 * Gdzie używać:
 * - Handlery / use-case’y / pipeline behaviors (Application)
 * - Integracje: mapowanie błędów z zewnątrz na własne ErrorData
 *
 * Gdzie raczej nie pchać:
 * - encje domeny / modele infrastruktury (tam inne mechanizmy i odpowiedzialności)
 */
#endregion

/// <summary>
/// Uproszczony status wyniku (pochodny od Errors/Warn).
/// </summary>
public enum MessageResultStatus
{
    /// <summary>Brak błędów i brak ostrzeżeń.</summary>
    Success = 0,

    /// <summary>Brak błędów, ale są ostrzeżenia (success + warnings).</summary>
    Partial = 1,

    /// <summary>Są błędy.</summary>
    Failure = 2
}

/// <summary>
/// Diagnostyczny hint dla warstwy Presentation (middleware).
/// To NIE jest HTTP ani domena — jedynie sygnał kontrolujący logowanie request/response body.
/// </summary>
public enum BodyLogPolicy
{
    /// <summary>Domyślna polityka (np. loguj body tylko dla 5xx).</summary>
    Default = 0,

    /// <summary>Wymuś logowanie body niezależnie od statusu HTTP (np. handler złapał wyjątek i zwrócił kontrolowany fail).</summary>
    Force = 1,

    /// <summary>Wyłącz logowanie body dla tego requestu.</summary>
    Suppress = 2
}

/// <summary>
/// Dodatkowe metadane wyniku (np. dla diagnostyki).
/// </summary>
public readonly record struct MessageResultDiagnostics(BodyLogPolicy BodyLogPolicy = BodyLogPolicy.Default);

public class MessageResult
{
    protected MessageResult(
        IReadOnlyList<ErrorData>? errors = null,
        IReadOnlyList<ErrorData>? warnings = null,
        MessageResultDiagnostics diagnostics = default)
    {
        Errors = Freeze(errors);
        Warnings = Freeze(warnings);
        Diagnostics = diagnostics;
    }

    public MessageResultDiagnostics Diagnostics { get; }

    // Status NIE jest już ustawiany ręcznie – wynika z danych.
    public MessageResultStatus Status =>
        Errors.Count > 0 ? MessageResultStatus.Failure :
        Warnings.Count > 0 ? MessageResultStatus.Partial :
        MessageResultStatus.Success;

    public bool IsSuccess => Errors.Count == 0;
    public bool IsPartial => Errors.Count == 0 && Warnings.Count > 0;
    public bool HasFailed => Errors.Count > 0;

    public IReadOnlyList<ErrorData> Errors { get; }
    public IReadOnlyList<ErrorData> Warnings { get; }

    public ErrorData? PrimaryError => Errors.Count > 0 ? Errors[0] : null;

    public virtual MessageResult WithDiagnostics(MessageResultDiagnostics diagnostics)
        => new(Errors, Warnings, diagnostics);

    public virtual MessageResult ForceBodyLogging()
        => WithDiagnostics(Diagnostics with { BodyLogPolicy = BodyLogPolicy.Force });

    public virtual MessageResult SuppressBodyLogging()
        => WithDiagnostics(Diagnostics with { BodyLogPolicy = BodyLogPolicy.Suppress });

    public static MessageResult Ok() => new();

    public static MessageResult Ok(params ErrorData[] warnings)
        => new(errors: null, warnings: warnings ?? Array.Empty<ErrorData>());

    // kompatybilność – dawniej Partial(...) tworzył status Partial,
    // teraz Partial wynika z warnings.
    public static MessageResult Partial(params ErrorData[] warnings)
        => Ok(warnings);

    public static MessageResult Fail(ErrorData error)
        => new(errors: [error]);

    public static MessageResult Fail(IEnumerable<ErrorData> errors)
        => new(errors: errors?.ToArray() ?? Array.Empty<ErrorData>());

    //// --- helpery dla MediatR pipeline / spójnego API ---
    public static MessageResult<T> Ok<T>(T value) => MessageResult<T>.Ok(value);

    public static MessageResult<T> Ok<T>(T value, params ErrorData[] warnings)
        => MessageResult<T>.Ok(value, warnings);

    public static MessageResult<T> Partial<T>(T value, params ErrorData[] warnings)
        => MessageResult<T>.Ok(value, warnings);

    public static MessageResult<T> Fail<T>(ErrorData error) => MessageResult<T>.Fail(error);

    public static MessageResult<T> Fail<T>(IEnumerable<ErrorData> errors) => MessageResult<T>.Fail(errors);

    private static IReadOnlyList<ErrorData> Freeze(IReadOnlyList<ErrorData>? src)
        => src is null || src.Count == 0
            ? Array.Empty<ErrorData>()
            : src is ErrorData[] a ? a : src.ToArray();
}