using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.Modules.Transport.Application;

/// <summary>
/// Katalog błędów aplikacji. Stabilny kontrakt: Code + Key + Args.
/// Description możesz trzymać jako PL fallback (tymczasowo).
///
/// UWAGA: Kind jest używany do mapowania na HTTP (w Presentation),
/// ale jest JsonIgnore => NIE wypływa do klienta.
/// </summary>
public static class Errors
{
    public static class Transport
    {
        public static readonly ErrorData ApiFailed = new(
            code: 4001,
            key: "errors.transport.api_failed",
            description: "Wywołanie zewnętrznego Transport API nie powiodło się.",
            kind: ErrorKind.DependencyFailure);

        // traktujemy jako upstream-timeout/cancel
        public static readonly ErrorData ApiCanceled = new(
            code: 4002,
            key: "errors.transport.api_canceled",
            description: "Wywołanie Transport API zostało anulowane.",
            kind: ErrorKind.DependencyTimeout);

        public static readonly ErrorData ApiException = new(
            code: 4003,
            key: "errors.transport.api_exception",
            description: "Wywołanie Transport API nie powiodło się z powodu wyjątku.",
            kind: ErrorKind.DependencyFailure);

        // warning (partial success)
        public static ErrorData DispatchFailed(string correlationId) => new(
            code: 9001,
            key: "warnings.transport.dispatch_failed",
            description: "Zamówienie utworzone, ale wysyłka do transportu nie powiodła się.",
            args: [correlationId],
            kind: ErrorKind.DependencyFailure);
    }
}