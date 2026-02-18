namespace MyApp.Api.Middleware;

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

    public bool Enabled { get; init; } = true;

    public BodyLoggingMode Mode { get; init; } = BodyLoggingMode.OnError;

    public int MaxBytes { get; init; } = 4096;

    // protection against big uploads (avoid buffering huge request bodies)
    public long MaxRequestContentLengthToCapture { get; init; } = 32_768;

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
