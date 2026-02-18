using MyApp.Domain.Common;

namespace MyApp.Application.Common;

/// <summary>
/// Katalog błędów aplikacji. Stabilny kontrakt: Code + Key + Args.
/// Description możesz trzymać jako PL fallback (tymczasowo).
/// </summary>
public static class Errors
{
    public static class Orders
    {
        public static ErrorData NotFound(int id) => new(
            code: 2001,
            key: "errors.orders.not_found",
            description: "Nie znaleziono zamówienia.", // fallback PL
            args: [id]);

        public static readonly ErrorData EmptyItems = new(
            code: 2002,
            key: "errors.orders.empty_items",
            description: "Zamówienie musi zawierać co najmniej jedną pozycję.");
    }

    public static class Customers
    {
        public static ErrorData NotFound(int id) => new(
            code: 2101,
            key: "errors.customers.not_found",
            description: "Nie znaleziono klienta.",
            args: [id]);
    }

    public static class Validation
    {
        public static readonly ErrorData Failed = new(
            code: 1000,
            key: "errors.validation.failed",
            description: "Walidacja nie powiodła się.");

        // --- Field-level validation rules (stabilny kontrakt: key + args) ---

        /// <summary>Fallback, gdy nie potrafimy zmapować konkretnej reguły.</summary>
        public static ErrorData UnknownRule(string field, string? ruleCode = null) => new(
            code: 1001,
            key: "errors.validation.unknown_rule",
            description: "Nieprawidłowa wartość pola.",
            args: ruleCode is null ? [field] : [field, ruleCode]);

        public static ErrorData Required(string field) => new(
            code: 1001,
            key: "errors.validation.required",
            description: "Pole jest wymagane.",
            args: [field]);

        public static ErrorData GreaterThan(string field, object? minExclusive) => new(
            code: 1001,
            key: "errors.validation.greater_than",
            description: "Wartość musi być większa niż podana.",
            args: [field, minExclusive]);

        public static ErrorData GreaterOrEqual(string field, object? minInclusive) => new(
            code: 1001,
            key: "errors.validation.greater_or_equal",
            description: "Wartość musi być większa lub równa podanej.",
            args: [field, minInclusive]);

        public static ErrorData LessThan(string field, object? maxExclusive) => new(
            code: 1001,
            key: "errors.validation.less_than",
            description: "Wartość musi być mniejsza niż podana.",
            args: [field, maxExclusive]);

        public static ErrorData LessOrEqual(string field, object? maxInclusive) => new(
            code: 1001,
            key: "errors.validation.less_or_equal",
            description: "Wartość musi być mniejsza lub równa podanej.",
            args: [field, maxInclusive]);

        public static ErrorData Between(string field, object? fromInclusive, object? toInclusive) => new(
            code: 1001,
            key: "errors.validation.between",
            description: "Wartość musi mieścić się w zakresie.",
            args: [field, fromInclusive, toInclusive]);

        public static ErrorData MinLength(string field, object? minLength) => new(
            code: 1001,
            key: "errors.validation.min_length",
            description: "Tekst jest zbyt krótki.",
            args: [field, minLength]);

        public static ErrorData MaxLength(string field, object? maxLength) => new(
            code: 1001,
            key: "errors.validation.max_length",
            description: "Tekst jest zbyt długi.",
            args: [field, maxLength]);

        public static ErrorData LengthBetween(string field, object? minLength, object? maxLength) => new(
            code: 1001,
            key: "errors.validation.length_between",
            description: "Tekst ma nieprawidłową długość.",
            args: [field, minLength, maxLength]);

        public static ErrorData Email(string field) => new(
            code: 1001,
            key: "errors.validation.email",
            description: "Nieprawidłowy adres email.",
            args: [field]);

        public static ErrorData PrecisionScale(string field, object? precision, object? scale) => new(
            code: 1001,
            key: "errors.validation.precision_scale",
            description: "Nieprawidłowa precyzja/liczba miejsc po przecinku.",
            args: [field, precision, scale]);

        // legacy (jeśli gdzieś jeszcze używasz):
        public static ErrorData Field(string field, string message) => new(
            code: 1001,
            key: "errors.validation.field",
            description: $"{field}: {message}",
            args: [field, message]);
    }

    public static class Db
    {
        public static readonly ErrorData Conflict = new(
            code: 3001,
            key: "errors.db.conflict",
            description: "Konflikt podczas zapisu zmian.");

        public static readonly ErrorData Unexpected = new(
            code: 3002,
            key: "errors.db.unexpected",
            description: "Nieoczekiwany błąd bazy danych.");

        public static readonly ErrorData PendingChanges = new(
            code: 3003,
            key: "errors.db.pending_changes",
            description: "Wywołano commit, mimo że istnieją niezapisane zmiany. Zapisz zmiany (SaveChangesAsync) albo użyj CommitWithSaveAsync.");

        // user-friendly
        public static readonly ErrorData Duplicate = new(
            code: 3004,
            key: "errors.db.duplicate",
            description: "Taki rekord już istnieje.");

        // user-friendly
        public static readonly ErrorData ForeignKey = new(
            code: 3005,
            key: "errors.db.foreign_key",
            description: "Nie można wykonać operacji, ponieważ istnieją powiązane dane.");

        public static readonly ErrorData ExecutionStrategyRequiresExecuteInTransaction = new(
            code: 3006,
            key: "errors.db.execution_strategy_requires_execute_in_transaction",
            description: "Włączono retry dla bazy (EnableRetryOnFailure). Ręczne transakcje (BeginTransaction) nie są wspierane. Użyj IUnitOfWork.ExecuteInTransactionAsync(...).");
    }

    public static class Transport
    {
        public static readonly ErrorData ApiFailed = new(
            code: 4001,
            key: "errors.transport.api_failed",
            description: "Wywołanie zewnętrznego Transport API nie powiodło się.");

        public static readonly ErrorData ApiCanceled = new(
            code: 4002,
            key: "errors.transport.api_canceled",
            description: "Wywołanie Transport API zostało anulowane.");

        // warning (partial success)
        public static ErrorData DispatchFailed(string correlationId) => new(
            code: 9001,
            key: "warnings.transport.dispatch_failed",
            description: "Zamówienie utworzone, ale wysyłka do transportu nie powiodła się.",
            args: [correlationId]);

        public static readonly ErrorData ApiException = new(
            code: 4003,
            key: "errors.transport.api_exception",
            description: "Wywołanie Transport API nie powiodło się z powodu wyjątku.");
    }

    public static class Common
    {
        public static readonly ErrorData Unexpected = new(
            code: 5000,
            key: "errors.unexpected",
            description: "Wystąpił nieoczekiwany błąd.");
    }
}
