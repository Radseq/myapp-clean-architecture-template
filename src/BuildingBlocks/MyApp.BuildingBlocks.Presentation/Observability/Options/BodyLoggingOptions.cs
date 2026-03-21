namespace MyApp.BuildingBlocks.Presentation.Observability.Options;

public enum BodyLoggingMode
{
	Off = 0,
	OnError = 1,       // >= 400
	OnServerError = 2, // >= 500
	Always = 3
}

public sealed class BodyLoggingOptions
{
	public const string SectionName = "Observability:BodyLogging";

	/// <summary>
	/// Template default: OFF.
	/// Włączasz świadomie (np. na czas incidentu) żeby nie płacić kosztu per-request i nie ryzykować PII.
	/// </summary>
	public bool Enabled { get; init; } = false;

	/// <summary>
	/// Template default: tylko 5xx.
	/// 4xx bywa “normalnym” ruchem (walidacje/401/403) i potrafi zalać store.
	/// </summary>
	public BodyLoggingMode Mode { get; init; } = BodyLoggingMode.OnServerError;

	/// <summary>Limit na request/response body (per strona) trzymany w pamięci.</summary>
	public int MaxBytes { get; init; } = 4096;

	/// <summary>
	/// Ochrona przed dużymi uploadami – nie buforujemy wielkich requestów.
	/// </summary>
	public long MaxRequestContentLengthToCapture { get; init; } = 32_768;

	/// <summary>
	/// Jeśli Content-Length jest nieznany (chunked), domyślnie NIE włączamy bufferowania.
	/// Możesz włączyć świadomie, akceptując ryzyko większych payloadów.
	/// </summary>
	public bool AllowUnknownContentLength { get; init; } = false;

	public string[] ContentTypesAllowList { get; init; } =
	[
		"application/json",
		"application/problem+json",
		"text/plain"
	];

	// this is NOT json-path in this implementation; it’s “deny property names anywhere in JSON”
	public string[] JsonDenyPaths { get; init; } =
	[
		"password",
		"token",
		"access_token",
		"refresh_token",
		"client_secret"
	];

	public StoreOptions Store { get; init; } = new();

	public sealed class StoreOptions
	{
		public string Mode { get; init; } = "Redis"; // Redis | Sql | None
		public int TtlMinutes { get; init; } = 60;
		public string KeyPrefix { get; init; } = "failed-http";
	}
}
