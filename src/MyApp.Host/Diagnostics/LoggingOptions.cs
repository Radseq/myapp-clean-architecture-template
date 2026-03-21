namespace MyApp.Host.Diagnostics;

/// <summary>
/// Konfiguracja "bootstrap" dla logowania w stylu produkcyjnym.
/// - W Kubernetes preferujemy stdout (JSON) i brak logów do plików.
/// - Na Windows (host/service) domyślnie włączamy logowanie do plików (rolling).
///
/// Uwaga:
/// - Poziomy logów filtruj standardowo przez "Logging:LogLevel" w appsettings*.json.
/// - Ten moduł odpowiada za provider (NLog), format (JSON) i routing (stdout + opcjonalnie plik).
/// </summary>
public sealed class LoggingOptions
{
	public const string SectionName = "Observability:Logging";

	/// <summary>Jeśli false - nie konfigurujemy NLog i zostawiasz własne logowanie.</summary>
	public bool UseNLog { get; init; } = true;

	/// <summary>Nazwa pliku konfiguracyjnego NLog (domyślnie w root aplikacji).</summary>
	public string NLogConfigFile { get; init; } = "nlog.config";

	/// <summary>
	/// Wymusza logowanie do pliku.
	/// null => AUTO (Windows + nie-container => true, w pozostałych => false)
	/// </summary>
	public bool? FileEnabled { get; init; }

	/// <summary>
	/// Katalog na logi plikowe. Jeśli null => {basedir}/logs (ustawiane jako env var dla NLog).
	/// </summary>
	public string? LogDirectory { get; init; }

	/// <summary>Maks. liczba plików archiwalnych (rolling).</summary>
	public int FileMaxArchiveFiles { get; init; } = 14;
}
