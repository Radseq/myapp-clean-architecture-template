using System.Text.Json.Serialization;

namespace MyApp.BuildingBlocks.Domain.Common;

/// <summary>
/// Transportowalny błąd/ostrzeżenie.
/// Stabilny kontrakt: Code + Key + Args.
/// Description opcjonalne (fallback; może być uzupełnione na API boundary).
/// </summary>
public sealed record ErrorData
{
	public int Code { get; init; } = -1;
	public string Key { get; init; } = "errors.unknown";
	public string? Description { get; init; }
	public object?[] Args { get; init; } = [];
	public IReadOnlyList<ErrorData> ExtendedErrors { get; init; } = [];

	[JsonIgnore]
	public ErrorKind Kind { get; init; } = ErrorKind.Unknown;

	public ErrorData() { } // dla serializacji

	public ErrorData(
		int code,
		string key,
		string? description = null,
		object?[]? args = null,
		IReadOnlyList<ErrorData>? extendedErrors = null,
		ErrorKind kind = ErrorKind.Unknown)
	{
		Code = code;
		Key = key;
		Description = description;
		Args = args ?? Array.Empty<object?>();
		ExtendedErrors = extendedErrors ?? Array.Empty<ErrorData>();
		Kind = kind;
	}

	public ErrorData WithKind(ErrorKind kind)
		=> this with { Kind = kind };

	public ErrorData WithArgs(params object?[] args)
		=> this with { Args = args ?? Array.Empty<object?>() };

	public ErrorData WithDescription(string? description)
		=> this with { Description = description };

	public ErrorData WithDescriptionIfMissing(string? description)
		=> string.IsNullOrWhiteSpace(Description)
			? this with { Description = description }
			: this;

	public ErrorData WithExtended(params ErrorData[] details)
		=> details is null || details.Length == 0
			? this
			: this with { ExtendedErrors = ExtendedErrors.Concat(details).ToArray() };
}