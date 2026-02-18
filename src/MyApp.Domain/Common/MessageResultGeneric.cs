namespace MyApp.Domain.Common;

public sealed class MessageResult<T> : MessageResult
{
    private MessageResult(
        MessageResultStatus status,
        T? value,
        IReadOnlyList<ErrorData>? errors = null,
        IReadOnlyList<ErrorData>? warnings = null)
        : base(status, errors, warnings)
    {
        Value = value;
    }

    public T? Value { get; }

    public static MessageResult<T> Ok(T value)
        => new(MessageResultStatus.Success, value);

    public static MessageResult<T> Partial(T value, params ErrorData[] warnings)
        => new(MessageResultStatus.Partial, value, warnings: warnings ?? []);

    public static MessageResult<T> Fail(ErrorData error)
        => new(MessageResultStatus.Failure, default, errors: [error]);

    public static MessageResult<T> Fail(IEnumerable<ErrorData> errors)
        => new(MessageResultStatus.Failure, default, errors: errors?.ToArray() ?? []);
}
