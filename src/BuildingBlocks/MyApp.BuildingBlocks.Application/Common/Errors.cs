using MyApp.BuildingBlocks.Domain.Common;

namespace MyApp.BuildingBlocks.Application.Common;

/// <summary>
/// Katalog błędów aplikacji. Stabilny kontrakt: Code + Key + Args.
/// Description możesz trzymać jako PL fallback (tymczasowo).
///
/// UWAGA: Kind jest używany do mapowania na HTTP (w Presentation),
/// ale jest JsonIgnore => NIE wypływa do klienta.
/// </summary>
public static class Errors
{

	public static class Validation
	{
		public static readonly ErrorData Failed = new(
			code: 1000,
			key: "errors.validation.failed",
			description: "Walidacja nie powiodła się.",
			kind: ErrorKind.Validation);

		// --- Field-level validation rules (stabilny kontrakt: key + args) ---

		/// <summary>Fallback, gdy nie potrafimy zmapować konkretnej reguły.</summary>
		public static ErrorData UnknownRule(string field, string? ruleCode = null) => new(
			code: 1001,
			key: "errors.validation.unknown_rule",
			description: "Nieprawidłowa wartość pola.",
			args: ruleCode is null ? [field] : [field, ruleCode],
			kind: ErrorKind.Validation);

		public static ErrorData Required(string field) => new(
			code: 1001,
			key: "errors.validation.required",
			description: "Pole jest wymagane.",
			args: [field],
			kind: ErrorKind.Validation);

		public static ErrorData GreaterThan(string field, object? minExclusive) => new(
			code: 1001,
			key: "errors.validation.greater_than",
			description: "Wartość musi być większa niż podana.",
			args: [field, minExclusive],
			kind: ErrorKind.Validation);

		public static ErrorData GreaterOrEqual(string field, object? minInclusive) => new(
			code: 1001,
			key: "errors.validation.greater_or_equal",
			description: "Wartość musi być większa lub równa podanej.",
			args: [field, minInclusive],
			kind: ErrorKind.Validation);

		public static ErrorData LessThan(string field, object? maxExclusive) => new(
			code: 1001,
			key: "errors.validation.less_than",
			description: "Wartość musi być mniejsza niż podana.",
			args: [field, maxExclusive],
			kind: ErrorKind.Validation);

		public static ErrorData LessOrEqual(string field, object? maxInclusive) => new(
			code: 1001,
			key: "errors.validation.less_or_equal",
			description: "Wartość musi być mniejsza lub równa podanej.",
			args: [field, maxInclusive],
			kind: ErrorKind.Validation);

		public static ErrorData Between(string field, object? fromInclusive, object? toInclusive) => new(
			code: 1001,
			key: "errors.validation.between",
			description: "Wartość musi mieścić się w zakresie.",
			args: [field, fromInclusive, toInclusive],
			kind: ErrorKind.Validation);

		public static ErrorData MinLength(string field, object? minLength) => new(
			code: 1001,
			key: "errors.validation.min_length",
			description: "Tekst jest zbyt krótki.",
			args: [field, minLength],
			kind: ErrorKind.Validation);

		public static ErrorData MaxLength(string field, object? maxLength) => new(
			code: 1001,
			key: "errors.validation.max_length",
			description: "Tekst jest zbyt długi.",
			args: [field, maxLength],
			kind: ErrorKind.Validation);

		public static ErrorData LengthBetween(string field, object? minLength, object? maxLength) => new(
			code: 1001,
			key: "errors.validation.length_between",
			description: "Tekst ma nieprawidłową długość.",
			args: [field, minLength, maxLength],
			kind: ErrorKind.Validation);

		public static ErrorData Email(string field) => new(
			code: 1001,
			key: "errors.validation.email",
			description: "Nieprawidłowy adres email.",
			args: [field],
			kind: ErrorKind.Validation);

		public static ErrorData PrecisionScale(string field, object? precision, object? scale) => new(
			code: 1001,
			key: "errors.validation.precision_scale",
			description: "Nieprawidłowa precyzja/liczba miejsc po przecinku.",
			args: [field, precision, scale],
			kind: ErrorKind.Validation);

		// legacy (jeśli gdzieś jeszcze używasz):
		public static ErrorData Field(string field, string message) => new(
			code: 1001,
			key: "errors.validation.field",
			description: $"{field}: {message}",
			args: [field, message],
			kind: ErrorKind.Validation);
	}

	public static class Db
	{
		// typowe "user facing" konflikty
		public static readonly ErrorData Conflict = new(
			code: 3001,
			key: "errors.db.conflict",
			description: "Konflikt podczas zapisu zmian.",
			kind: ErrorKind.Conflict);

		public static readonly ErrorData Duplicate = new(
			code: 3004,
			key: "errors.db.duplicate",
			description: "Taki rekord już istnieje.",
			kind: ErrorKind.Conflict);

		public static readonly ErrorData ForeignKey = new(
			code: 3005,
			key: "errors.db.foreign_key",
			description: "Nie można wykonać operacji, ponieważ istnieją powiązane dane.",
			kind: ErrorKind.Conflict);

		// bardziej "500" / programistyczne / infrastrukturalne
		public static readonly ErrorData Unexpected = new(
			code: 3002,
			key: "errors.db.unexpected",
			description: "Nieoczekiwany błąd bazy danych.",
			kind: ErrorKind.Unexpected);

		public static readonly ErrorData PendingChanges = new(
			code: 3003,
			key: "errors.db.pending_changes",
			description: "Wywołano commit, mimo że istnieją niezapisane zmiany. Zapisz zmiany (SaveChangesAsync) albo użyj CommitWithSaveAsync.",
			kind: ErrorKind.Unexpected);

		public static readonly ErrorData ExecutionStrategyRequiresExecuteInTransaction = new(
			code: 3006,
			key: "errors.db.execution_strategy_requires_execute_in_transaction",
			description: "Włączono retry dla bazy (EnableRetryOnFailure). Ręczne transakcje (BeginTransaction) nie są wspierane. Użyj IUnitOfWork.ExecuteInTransactionAsync(...).",
			kind: ErrorKind.Unexpected);
	}

	public static class Common
	{
		public static readonly ErrorData Unexpected = new(
			code: 5000,
			key: "errors.unexpected",
			description: "Wystąpił nieoczekiwany błąd.",
			kind: ErrorKind.Unexpected);
	}
}