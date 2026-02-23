namespace MyApp.Domain.Common;

/*
MessageResult u Ciebie to „kontrolowany wynik use-case’a”, który zastępuje wyjątki jako flow i daje spójny kontrakt błędów/ostrzeżeń między warstwami Application ↔ API (i ewentualnie między modułami), a dopiero na boundary HTTP jest mapowany na ApiResponse<T> albo ApiProblemDetails.

O co chodzi z MessageResult (sens i rola)
1) Komunikacja między warstwami (nie tylko “serwisami”)

Najważniejsze: MessageResult to standardowy typ wyniku dla logiki aplikacyjnej (handlers / use-case’y), np.:

Ok(value) – sukces

Fail(errors) – porażka z listą błędów

Partial(value, warnings) – sukces częściowy (ważne u Ciebie)

To pozwala:

nie rzucać wyjątków do sterowania logiką,

mieć jeden sposób propagacji błędów z dowolnego miejsca w Application,

mieć pipeline behaviors (Validation/UoW/Logging) działające jednolicie.

2) Stabilny kontrakt błędu: Code + Key + Args

W praktyce najważniejsze w ErrorData to:

Code – stabilny numer (logi, mapowanie statusów, analityka)

Key – klucz do lokalizacji (errors.orders.not_found)

Args – argumenty do wstawienia (np. id)

Description traktujesz jako fallback (np. PL) — ale docelowo można go nawet nie wymagać, bo i tak boundary może go uzupełnić.

3) Lokalizacja (RESX) + fallback, dokładnie jak pamiętasz

Flow u Ciebie jest taki:

Handler zwraca np.
return MessageResult.Fail(Errors.Orders.NotFound(id));

API boundary (Twoja fabryka ProblemDetails / extensiony) robi:

bierze ErrorData z resultu,

próbuje znaleźć opis w Errors.pl-PL.resx / Errors.en-US.resx po Key,

jeśli znajdzie → ustawia Description na tekst z RESX (z formatowaniem Args),

jeśli nie znajdzie → zostawia fallback z ErrorData.Description,

jeśli i tego brak → finalny fallback to Key.

Czyli dokładnie: RESX > Description > Key.

4) Po co to klientowi?

Klient dostaje:

albo ApiResponse<T> (sukces + warnings),

albo ApiProblemDetails (błąd + ewentualne warnings, correlationId, traceId).

I w obu przypadkach w środku ma listy ErrorData/WarningData:

może wyświetlać Description (już zlokalizowane przez serwer),

może też użyć Code/Key/Args (np. gdy chcesz lokalizować po stronie klienta).

Ważna różnica: MessageResult vs ProblemDetails

MessageResult to wewnętrzny kontrakt aplikacji (Application).

ApiProblemDetails to kontrakt HTTP (API boundary).

“Seniorowo” pilnujesz, żeby:

domena/aplikacja nie znały HTTP,

mapowanie na statusy i ProblemDetails było tylko w warstwie API.

Partial success (u Ciebie kluczowe)

To jest dokładnie przypadek:
“zamówienie zapisane w DB, ale wysyłka do innego API się nie udała”.

Wtedy handler robi:

Ok/Created + Warnings (np. Warnings.Transport.DispatchFailed(correlationId))

I API:

przy create-with-transport: może zwrócić 201 z warnings,

przy create (NoContent): 204 tylko przy czystym sukcesie, a przy warnings → 200 + ApiResponse(null, warnings).

To jest powód, dla którego MessageResult jest Ci potrzebny: bez tego zwykle kończy się na “albo wyjątek, albo 204”, a Ty chcesz stan pośredni.

Gdzie MessageResult ma sens, a gdzie nie

Ma sens:

Handlery MediatR (Application)

Serwisy aplikacyjne (Application)

Pipeline behaviors (walidacja/tx/logowanie)

Integracje, gdy chcesz “przetłumaczyć” błędy z zewnątrz na swoje ErrorData

Nie pchałbym go:

do EF encji / domeny (tam raczej reguły domenowe i ewentualnie Result dla fabryk),

do modeli infrastruktury,

jako “uniwersalny wrapper” wszędzie (np. repozytoria często lepiej zwracają null/bool/Entity? i to handler mapuje na MessageResult — zależy od stylu, ale ważna jest konsekwencja).
*/

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