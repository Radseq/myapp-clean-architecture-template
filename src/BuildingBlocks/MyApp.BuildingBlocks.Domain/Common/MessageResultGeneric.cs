namespace MyApp.BuildingBlocks.Domain.Common;

public sealed class MessageResult<T> : MessageResult
{
	private MessageResult(
		T? value,
		IReadOnlyList<ErrorData>? errors = null,
		IReadOnlyList<ErrorData>? warnings = null,
		MessageResultDiagnostics diagnostics = default)
		: base(errors, warnings, diagnostics)
	{
		Value = value;
	}

	public T? Value { get; }

	public override MessageResult<T> WithDiagnostics(MessageResultDiagnostics diagnostics)
		=> new(Value, Errors, Warnings, diagnostics);

	public override MessageResult<T> ForceBodyLogging()
		=> WithDiagnostics(Diagnostics with { BodyLogPolicy = BodyLogPolicy.Force });

	public override MessageResult<T> SuppressBodyLogging()
		=> WithDiagnostics(Diagnostics with { BodyLogPolicy = BodyLogPolicy.Suppress });

	public static MessageResult<T> Ok(T value)
		=> new(value);

	public static MessageResult<T> Ok(T value, params ErrorData[] warnings)
		=> new(value, errors: null, warnings: warnings ?? Array.Empty<ErrorData>());

	public static MessageResult<T> Partial(T value, params ErrorData[] warnings)
		=> Ok(value, warnings);

	public static MessageResult<T> Fail(ErrorData error)
		=> new(default, errors: [error]);

	public static MessageResult<T> Fail(IEnumerable<ErrorData> errors)
		=> new(default, errors: errors?.ToArray() ?? Array.Empty<ErrorData>());
}