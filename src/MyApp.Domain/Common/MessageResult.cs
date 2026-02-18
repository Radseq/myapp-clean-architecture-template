namespace MyApp.Domain.Common;

public enum MessageResultStatus
{
    Success = 0,
    Partial = 1, // sukces + ostrzeżenia
    Failure = 2
}


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

public class MessageResult
{
    protected MessageResult(
        MessageResultStatus status,
        IReadOnlyList<ErrorData>? errors = null,
        IReadOnlyList<ErrorData>? warnings = null)
    {
        Status = status;
        Errors = errors is null ? Array.Empty<ErrorData>() : errors.ToArray();
        Warnings = warnings is null ? Array.Empty<ErrorData>() : warnings.ToArray();
    }

    public MessageResultStatus Status { get; }

    public bool IsSuccess => Status is MessageResultStatus.Success or MessageResultStatus.Partial;
    public bool IsPartial => Status == MessageResultStatus.Partial;
    public bool HasFailed => Status == MessageResultStatus.Failure;

    public IReadOnlyList<ErrorData> Errors { get; }
    public IReadOnlyList<ErrorData> Warnings { get; }

    public ErrorData? PrimaryError => Errors.Count > 0 ? Errors[0] : null;

    public static MessageResult Ok() => new(MessageResultStatus.Success);

    public static MessageResult Partial(params ErrorData[] warnings)
        => new(MessageResultStatus.Partial, warnings: warnings ?? Array.Empty<ErrorData>());

    public static MessageResult Fail(ErrorData error)
        => new(MessageResultStatus.Failure, errors: new[] { error });

    public static MessageResult Fail(IEnumerable<ErrorData> errors)
        => new(MessageResultStatus.Failure, errors: errors?.ToArray() ?? Array.Empty<ErrorData>());

    //// --- helpery dla MediatR pipeline / spójnego API ---
    public static MessageResult<T> Ok<T>(T value) => MessageResult<T>.Ok(value);

    public static MessageResult<T> Partial<T>(T value, params ErrorData[] warnings)
        => MessageResult<T>.Partial(value, warnings);

    public static MessageResult<T> Fail<T>(ErrorData error) => MessageResult<T>.Fail(error);

    public static MessageResult<T> Fail<T>(IEnumerable<ErrorData> errors) => MessageResult<T>.Fail(errors);
}
