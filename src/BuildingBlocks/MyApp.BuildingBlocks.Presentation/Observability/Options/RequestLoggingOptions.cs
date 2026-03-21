using MyApp.BuildingBlocks.Presentation.Observability.Middleware;

namespace MyApp.BuildingBlocks.Presentation.Observability.Options;

public sealed class RequestLoggingOptions
{
	public const string SectionName = "Observability:RequestLogging";

	/// <summary>Domyślnie OFF, bo query string często zawiera PII.</summary>
	public bool LogQueryString { get; init; } = false;

	/// <summary>Domyślnie OFF; jak ON to i tak tylko allowlist.</summary>
	public bool LogHeaders { get; init; } = false;

	public int MaxValueLength { get; init; } = 256;

	/// <summary>
	/// Jeśli lista niepusta -> logujemy TYLKO te nagłówki (najbezpieczniejsze).
	/// </summary>
	public string[] HeaderAllowList { get; init; } =
	[
		"User-Agent",
		"Accept",
		"Accept-Language",
		CorrelationIdMiddleware.HeaderName
	];

	/// <summary>Deny zawsze wygrywa.</summary>
	public string[] HeaderDenyList { get; init; } =
	[
		"Authorization",
		"Cookie",
		"Set-Cookie",
		"X-Api-Key"
	];

	public string[] QueryStringDenyList { get; init; } =
	[
		"password",
		"token",
		"access_token",
		"refresh_token",
		"client_secret",
		"code"
	];
}
