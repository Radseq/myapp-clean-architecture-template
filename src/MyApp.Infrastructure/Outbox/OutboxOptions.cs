namespace MyApp.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public int BatchSize { get; init; } = 20;
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan LeaseTime { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxAttempts { get; init; } = 20;
    public TimeSpan MinBackoff { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(10);
}
