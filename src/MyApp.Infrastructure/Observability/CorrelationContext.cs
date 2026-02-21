using MyApp.Application.Abstractions.Observability;

namespace MyApp.Infrastructure.Observability;

public sealed class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public string? CorrelationId
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}